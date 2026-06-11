// StopMe — demo data (running apps, website categories, sessions, stats)

const RUNNING_APPS = [
  { id: "discord", name: "Discord", process: "discord.exe", path: "C:\\Users\\you\\AppData\\Local\\Discord", color: "#5865F2", category: "Social" },
  { id: "steam", name: "Steam", process: "steam.exe", path: "C:\\Program Files (x86)\\Steam", color: "#1B2838", category: "Gaming" },
  { id: "epic", name: "Epic Games Launcher", process: "EpicGamesLauncher.exe", path: "C:\\Program Files\\Epic Games", color: "#2F2D2E", category: "Gaming" },
  { id: "spotify", name: "Spotify", process: "spotify.exe", path: "C:\\Users\\you\\AppData\\Roaming\\Spotify", color: "#1DB954", category: "Music" },
  { id: "chrome", name: "Google Chrome", process: "chrome.exe", path: "C:\\Program Files\\Google\\Chrome", color: "#DB4437", category: "Browser" },
  { id: "slack", name: "Slack", process: "slack.exe", path: "C:\\Users\\you\\AppData\\Local\\slack", color: "#611F69", category: "Work" },
  { id: "telegram", name: "Telegram", process: "Telegram.exe", path: "C:\\Users\\you\\AppData\\Roaming\\Telegram", color: "#2AABEE", category: "Social" },
  { id: "riot", name: "Riot Client", process: "RiotClientServices.exe", path: "C:\\Riot Games\\Riot Client", color: "#D13639", category: "Gaming" },
  { id: "notion", name: "Notion", process: "Notion.exe", path: "C:\\Users\\you\\AppData\\Local\\Programs\\Notion", color: "#37352F", category: "Work" },
  { id: "whatsapp", name: "WhatsApp", process: "WhatsApp.exe", path: "C:\\Program Files\\WindowsApps\\WhatsApp", color: "#25D366", category: "Social" },
];

const SITE_CATEGORIES = [
  { id: "social", label: "Social Media", icon: "globe", sites: ["facebook.com", "instagram.com", "twitter.com", "x.com", "tiktok.com", "reddit.com", "linkedin.com/feed"] },
  { id: "video", label: "Video Streaming", icon: "play", sites: ["youtube.com", "netflix.com", "twitch.tv", "primevideo.com", "disneyplus.com"] },
  { id: "gaming", label: "Gaming", icon: "zap", sites: ["store.steampowered.com", "epicgames.com", "op.gg", "ign.com"] },
  { id: "news", label: "News", icon: "file", sites: ["cnn.com", "bbc.com", "lemonde.fr", "news.google.com"] },
  { id: "shopping", label: "Shopping", icon: "apps", sites: ["amazon.com", "ebay.com", "aliexpress.com", "vinted.fr"] },
  { id: "ai", label: "AI Chatbots", icon: "sparkle", sites: ["chatgpt.com", "claude.ai", "gemini.google.com", "character.ai"] },
];

const SITE_COLORS = {
  "youtube.com": "#FF0033", "instagram.com": "#C13584", "reddit.com": "#FF4500",
  "twitter.com": "#1DA1F2", "x.com": "#111111", "tiktok.com": "#161823",
  "facebook.com": "#1877F2", "netflix.com": "#E50914", "twitch.tv": "#9146FF",
  "chatgpt.com": "#10A37F", "claude.ai": "#D97757", "amazon.com": "#FF9900",
};

const PAST_SESSIONS = [
  { id: "p1", name: "Deep work", date: "Today, 9:00 AM", duration: 120, completed: true, strict: true, apps: 4, sites: 12 },
  { id: "p2", name: "Morning focus", date: "Yesterday, 8:30 AM", duration: 90, completed: true, strict: false, apps: 3, sites: 8 },
  { id: "p3", name: "Exam revision", date: "Mon, 2:00 PM", duration: 180, completed: false, stoppedAt: 124, strict: false, apps: 6, sites: 15 },
  { id: "p4", name: "Deep work", date: "Mon, 9:00 AM", duration: 120, completed: true, strict: true, apps: 4, sites: 12 },
];

const SCHEDULED_SESSIONS = [
  { id: "s1", name: "Evening study", when: "Today, 7:00 PM", duration: 90, strict: true, apps: 5, sites: 10 },
];

const RECURRING_SESSIONS = [
  { id: "r1", name: "Work mornings", days: ["Mon", "Tue", "Wed", "Thu", "Fri"], time: "9:00 AM – 12:00 PM", strict: false, apps: 4, sites: 12, enabled: true },
  { id: "r2", name: "No late-night gaming", days: ["Every day"], time: "11:00 PM – 7:00 AM", strict: true, apps: 3, sites: 4, enabled: false },
];

const STATS = {
  focusTimeWeek: "14 h 20 min",
  sessionsCompleted: 23,
  appsBlockedCount: 156,
  sitesBlockedCount: 489,
  topDistraction: "YouTube",
  streak: 6,
  focusByDay: [
    { day: "Mon", mins: 150 }, { day: "Tue", mins: 210 }, { day: "Wed", mins: 90 },
    { day: "Thu", mins: 240 }, { day: "Fri", mins: 180 }, { day: "Sat", mins: 0 }, { day: "Sun", mins: 60 },
  ],
  blocksByCategory: [
    { label: "Video Streaming", count: 212, color: "var(--accent)" },
    { label: "Social Media", count: 148, color: "#9C8DF0" },
    { label: "Gaming", count: 86, color: "#C9C0F7" },
    { label: "Other", count: 43, color: "#E3DEFB" },
  ],
  topBlocked: [
    { name: "YouTube", kind: "site", count: 134, color: "#FF0033" },
    { name: "Discord", kind: "app", count: 89, color: "#5865F2" },
    { name: "Reddit", kind: "site", count: 67, color: "#FF4500" },
    { name: "Steam", kind: "app", count: 41, color: "#1B2838" },
    { name: "Instagram", kind: "site", count: 38, color: "#C13584" },
  ],
};

const DIAGNOSTIC_CHECKS = [
  { id: "service", label: "StopMe background service", detail: "stopme-svc.exe" },
  { id: "ipc", label: "App ↔ service connection", detail: "Named pipe handshake" },
  { id: "auth", label: "Service authentication", detail: "Local token exchange" },
  { id: "ext-chrome", label: "Chrome extension", detail: "StopMe for Chrome v2.4.1" },
  { id: "ext-edge", label: "Edge extension", detail: "Not installed" },
  { id: "hosts", label: "Network filter", detail: "WFP filter driver" },
];

Object.assign(window, {
  RUNNING_APPS, SITE_CATEGORIES, SITE_COLORS,
  PAST_SESSIONS, SCHEDULED_SESSIONS, RECURRING_SESSIONS, STATS, DIAGNOSTIC_CHECKS,
});
