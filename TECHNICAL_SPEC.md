# StopMe — Spécification technique (Windows)

> Ce document explique **ce que l'app doit faire techniquement** et **comment le faire sur
> Windows**, en miroir exact de l'UI livrée dans ce handoff. Chaque écran/état de l'UI
> correspond à un mécanisme système décrit ici.

---

## 1. Architecture générale

StopMe est composé de **3 processus** qui coopèrent :

```
┌─────────────────────┐   Named pipe (JSON, token local)   ┌──────────────────────────┐
│  StopMe.exe (UI)    │◄──────────────────────────────────►│  stopme-svc.exe          │
│  WinUI 3 / WinAppSDK│                                     │  Service Windows          │
│  - Dashboard, pages │                                     │  - moteur de blocage apps │
│  - tray icon        │                                     │  - timer de session       │
└─────────────────────┘                                     │  - enforcement strict     │
                                                            │  - stats (SQLite)         │
┌─────────────────────┐   Native Messaging (stdio JSON)     │  - WFP fallback           │
│ Extension Chrome/   │◄───────────────────────────────────►│                          │
│ Edge (MV3)          │                                     └──────────────────────────┘
│ - blocage de sites  │
│ - page "blocked"    │
└─────────────────────┘
```

**Pourquoi un service ?** L'UI peut être fermée ; le blocage doit continuer. Le strict mode
doit survivre à la fermeture de l'app, à la déconnexion de session et au **reboot de Windows**.
Seul un service Windows (démarrage automatique, compte `LocalService` + privilèges ciblés)
garantit ça.

---

## 2. Le service Windows (`stopme-svc.exe`)

- **Installation** : service Win32 classique (`sc create` via l'installeur MSIX/MSI),
  `StartType = Automatic`, recovery configuré (`sc failure`) : redémarrage automatique après
  crash (1ʳᵉ, 2ᵉ, 3ᵉ défaillance → restart immédiat). C'est la 1ʳᵉ ligne de défense du strict mode.
- **État persisté** : la session active (JSON chiffré DPAPI machine-scope) dans
  `C:\ProgramData\StopMe\state.bin`. Au démarrage du service : si `endsAt > now`, le blocage
  reprend immédiatement (→ l'UI retrouve la session au reboot, comme le prototype via
  `localStorage`).
- **Horloge** : stocker `endsAt` en temps UTC + un compteur monotone (`QueryUnsupportedInterruptTime`
  / `GetTickCount64` au boot) pour détecter les manipulations d'horloge système ; en cas
  d'incohérence, conserver la durée restante calculée sur le temps monotone.

---

## 3. IPC App ↔ Service (les états "Connecting / Error" de l'UI)

- **Transport** : named pipe `\\.\pipe\stopme-ipc`, messages JSON (request/response + events
  push). SDDL du pipe : accès limité aux sessions interactives + au service.
- **Handshake** :
  1. L'UI ouvre le pipe → état UI **"Connecting"** (spinner).
  2. Échange de token : le service écrit un token aléatoire dans
     `%ProgramData%\StopMe\auth` (ACL : lisible par le groupe Users, écrit par le service) ;
     l'UI le renvoie dans `HELLO`. Échec → état UI **"Authentication issue"** → bouton
     *Repair connection* = régénérer le token + relancer le handshake.
  3. Réponse `HELLO_OK {version, sessionState}` → état UI **"Connected"**.
- **Timeouts** (mapping exact sur l'UI) :
  - 0–5 s sans réponse → banner *Connecting* (neutre).
  - &gt;5 s → banner *Still connecting…* (warning) + boutons Retry / Run diagnostics.
  - Pipe introuvable (`ERROR_FILE_NOT_FOUND`) ou `OpenSCManager`/`QueryServiceStatus` dit que le
    service est arrêté → état **"Service not running"** ; *Retry* = `StartService()` (demande
    d'élévation UAC si nécessaire) puis re-handshake.
- **Heartbeat** : ping toutes les 10 s ; la mini-card *Status* affiche "Last check : just now".
  2 pings manqués → retour à l'état Connecting + reconnexion automatique avec backoff.

---

## 4. Blocage d'applications (page **Block Apps**)

### 4.1 Lister les "Running apps"
- Énumération : `CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS)` → liste des processus, filtrée
  sur ceux qui ont **une fenêtre top-level visible** (`EnumWindows` + `GetWindowThreadProcessId`)
  pour ne montrer que des "apps" et pas 200 processus système — l'utilisateur ne doit jamais
  devoir comprendre ce qu'est un process.
- **Nom humain** (prioritaire dans l'UI) : ressource version de l'exe — `GetFileVersionInfo` →
  `FileDescription` (ex. "Discord") ; fallback : nom de l'exe sans extension.
- **Icône** : `SHGetFileInfo` / `ExtractIconEx` sur le chemin de l'exe → l'UI remplace les
  tuiles-lettres du prototype par les vraies icônes.
- **Refresh** : re-snapshot à la demande (bouton) — pas de polling continu côté UI.

### 4.2 Règles de blocage (badges de l'UI : Process / Path / Folder / Window title)
| Règle | Matching côté service |
|---|---|
| **Process** | nom d'image insensible à la casse (`discord.exe`) |
| **Path** | chemin complet normalisé de l'exe (`QueryFullProcessImageName`) |
| **Folder** | préfixe de chemin (tout exe sous `C:\Riot Games\` est bloqué) |
| **Window title** | sous-chaîne dans le titre (`EnumWindows` + `GetWindowText`), réévalué périodiquement |

### 4.3 Enforcement (pendant une session)
1. **Au démarrage de session** : tuer les processus qui matchent (`OpenProcess(PROCESS_TERMINATE)`
   + `TerminateProcess`). Tentative de fermeture gracieuse d'abord (`WM_CLOSE` aux fenêtres,
   2 s de grâce) pour ne pas corrompre les données.
2. **Empêcher la réouverture** : abonnement aux créations de processus —
   **WMI `Win32_ProcessStartTrace`** (simple) ou **ETW kernel process provider** (réactif,
   <100 ms). Chaque nouveau processus est matché contre les règles → kill immédiat +
   événement `APP_BLOCKED {name, ts}` poussé à l'UI.
3. **Notification** : sur `APP_BLOCKED`, l'UI (ou le service via `AppNotificationManager`)
   affiche le toast Windows du design (#14) : "*{App} is blocked — StopMe is protecting your
   focus. Available again at {endTime}.*" **Throttling** : 1 toast max par app par 5 min.

---

## 5. Blocage de sites web (page **Block Websites**)

### 5.1 Voie principale : extension navigateur (Chrome + Edge, MV3)
- L'extension reçoit la blocklist via **Native Messaging** (host manifest enregistré en
  HKLM, pointe vers un petit broker parlant au service).
- Blocage par **`declarativeNetRequest`** : règles dynamiques générées depuis la liste —
  domaines (`youtube.com` → `||youtube.com^`), chemins (`reddit.com/r/funny`), **wildcards**
  (`google.com/*q=*game*` → `urlFilter`). Les **exceptions** ("Allow these websites…") sont des
  règles `allow` de priorité supérieure.
- Au lieu de bloquer à vide : redirection vers la **page bloquée** du design (#15), une page
  embarquée dans l'extension (`chrome-extension://…/blocked.html?site=…&until=…`) — serif
  "This one can wait.", heure de fin, citation.
- **Détection de connexion** (état UI "Extension disconnected") : ping native-messaging
  toutes les 30 s ; absence de réponse → le service notifie l'UI → banner warning + CTA
  *Install extension* (lien vers le store).

### 5.2 Fallback : navigateurs sans extension (réglage "Unsupported browsers")
Trois comportements (Settings → Blocking) :
- **Close the browser tab** : surveiller les titres de fenêtres des navigateurs connus —
  si le titre matche un site bloqué, `WM_CLOSE` sur l'onglet (Ctrl+W simulé) ; approximatif.
- **Block the whole browser** : traiter le navigateur non supporté comme une app bloquée
  (règle Process temporaire pendant la session).
- **Allow** : ne rien faire (déconseillé, affiché comme tel).
- Option avancée (v2) : filtre **WFP** (Windows Filtering Platform) au niveau du service pour
  bloquer les résolutions DNS / connexions TLS par SNI — c'est le check "Network filter
  (WFP filter driver)" de la modal diagnostics. Pas de modification du fichier `hosts`
  (fragile, cassé par DoH).

---

## 6. Sessions, timer, scheduling (pages **Dashboard** / **Sessions**)

- **Modèle** :
  ```
  Session {
    id, name, strict: bool,
    durationMins, startedAt, endsAt (UTC),
    rules: { apps: AppRule[], sites: string[], exceptions: string[] },
    origin: manual | scheduled | recurring
  }
  ```
- **Démarrage** (`START_SESSION`) : le service valide (≥1 règle, pas de session en cours),
  persiste l'état, applique §4.3 + §5, répond avec la session → l'UI bascule sur le ring.
- **Countdown** : l'UI tick localement (1 s) sur `endsAt` ; le service est la source de vérité
  et pousse `SESSION_ENDED` à l'expiration (timer haute fiabilité côté service —
  `CreateWaitableTimer`, recalcul au resume du sleep via `PBT_APMRESUMEAUTOMATIC`).
- **Stop (normal)** : `STOP_SESSION` → le service lève les règles, log la session "stopped
  early" pour les stats. L'UI demande confirmation avant (modal).
- **Scheduled / Recurring** : stockés côté service ; un scheduler interne (vérification à la
  minute) démarre les sessions. Le service tournant en permanence, pas besoin de Task Scheduler.
  Une session récurrente désactivée (toggle UI) reste stockée mais ignorée.

---

## 7. Strict mode — enforcement réel

Le cadenas de l'UI n'est qu'un reflet ; **c'est le service qui garantit** :
1. `STOP_SESSION` sur une session strict → réponse `ERR_STRICT_LOCKED` (l'UI shake + tooltip).
   Aucune commande IPC ne peut terminer/modifier une session strict avant `endsAt`.
2. **Kill du service** : recovery SCM (restart auto) + au restart, l'état persisté re-applique
   le blocage en quelques secondes. L'arrêt du service via `services.msc` requiert admin ; en
   strict, le service refuse `SERVICE_CONTROL_STOP` (il signale `NOT_STOPPABLE` pendant la session).
3. **Reboot** : service auto-start + état persisté (§2) → la session reprend.
4. **Désinstallation** : l'uninstaller vérifie auprès du service qu'aucune session strict n'est
   active, sinon refuse poliment (même message que l'UI).
5. **Horloge** : temps monotone (§2) — avancer l'horloge ne termine pas la session.
6. Limite assumée (à documenter, pas à "résoudre") : un admin déterminé peut toujours gagner
   (safe mode, etc.). L'objectif est la **friction**, pas une sécurité absolue — ton produit
   est un outil de discipline, pas un EDR.

---

## 8. Statistiques (page **Statistics**)

- **Stockage** : SQLite dans `%ProgramData%\StopMe\stats.db`, écrit par le service.
  Tables : `sessions(id, name, strict, started, ended, completed)` et
  `block_events(ts, kind app|site, target, session_id)`.
- Les métriques de l'UI = requêtes simples : focus time (Σ durées), sessions completed,
  blocs par cible (top blocked, top distraction), streak (jours consécutifs avec ≥1 session).
- **Privacy** (Settings → Privacy) :
  - *Readable* : cibles stockées en clair.
  - *Anonymized* : `target` remplacé par un HMAC-SHA256 (clé locale) + catégorie — les counts
    et graphes par catégorie restent possibles, plus les noms.
  - *Disabled* : aucun `block_event` écrit ; les cartes de l'UI affichent des "—".
- **Export** : dump CSV des deux tables. **Delete data** : `DELETE` + `VACUUM`.

---

## 9. Diagnostics (modal **Service diagnostics**)

Chaque ligne de la modal correspond à un check réel exécuté séquentiellement (~0,5 s/check) :

| Check UI | Implémentation |
|---|---|
| StopMe background service | `QueryServiceStatusEx("StopMeSvc")` == RUNNING |
| App ↔ service connection | ouverture du pipe + `PING` aller-retour |
| Service authentication | handshake token complet (§3) |
| Chrome extension | `PING` native-messaging, timeout 2 s |
| Edge extension | idem (peut être "Not installed" → warning *Optional*) |
| Network filter | énumération des filtres WFP de StopMe (`FwpmFilterEnum`) |

Le CTA final dépend du premier échec : *Restart service* (`StartService`), *Repair connection*
(token), *Install extension* (store). "Open logs" → `explorer.exe` sur
`%ProgramData%\StopMe\logs\` (logs rotatifs du service, niveau info).

---

## 10. Intégration Windows (Settings → General / About)

- **Start at boot** : ce réglage concerne l'**UI** (le service démarre toujours tout seul) —
  clé `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` ou StartupTask MSIX.
- **Start minimized** : flag au lancement → fenêtre cachée, tray seulement.
- **Tray icon** : `NotifyIcon` (Shell_NotifyIcon) ; menu : état de la session (countdown),
  Open StopMe, Start quick session, Quit (désactivé pendant une session strict). Icône en
  surbrillance violette quand une session est active.
- **Title bar** : `ExtendsContentIntoTitleBar = true` + `SetTitleBar()` sur la zone de drag ;
  boutons caption custom 46 px conformes au design.
- **Data location** (About) : `%APPDATA%\StopMe` (préférences UI) et `%ProgramData%\StopMe`
  (état service, stats, logs).

---

## 11. Mapping rapide UI → backend

| Élément UI | Source technique |
|---|---|
| Chip sidebar "Service connected" | heartbeat IPC (§3) |
| Banner Connecting/Down/Auth/Ext | machine d'états du handshake (§3) |
| Liste "Running apps" | snapshot Toolhelp + version info + icônes (§4.1) |
| Toggle "Block" sur une app | ajoute/retire une `AppRule` (state UI, envoyé au start) |
| Start Blocking | `START_SESSION` (§6) |
| Ring countdown / mini-card sidebar | tick local sur `endsAt`, events `SESSION_*` du service |
| Stop locké (strict) | `ERR_STRICT_LOCKED` (§7) |
| Toast "{App} is blocked" | event `APP_BLOCKED` + AppNotification (§4.3) |
| Page navigateur bloquée | `blocked.html` de l'extension (§5.1) |
| Cartes Statistics | requêtes SQLite (§8) |
| Modal Diagnostics | suite de checks (§9) |

---

## 12. Ordre d'implémentation recommandé

1. Service squelette + IPC + handshake (débloque tous les états de connexion de l'UI).
2. Énumération des apps + règles + kill/watch (cœur du produit, page Block Apps).
3. Sessions + timer + persistence + strict (Dashboard complet).
4. Extension Chrome (Block Websites) + native messaging + page bloquée.
5. Stats SQLite + page Statistics.
6. Scheduling/recurring, fallback navigateurs, WFP (v2).
