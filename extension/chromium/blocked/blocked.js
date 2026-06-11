(() => {
  const params = new URLSearchParams(location.search);
  const url = params.get('url') ?? '';
  const remaining = Number.parseInt(params.get('remaining') ?? '', 10);

  // Pull the host out of the full URL so the visible site name reads as a domain
  // (e.g. "youtube.com") rather than a bare path.
  let host = url;
  try { host = new URL(url).host || url; } catch { /* keep raw */ }
  document.getElementById('site').textContent = host || 'This site';

  const formatTime = (date) => {
    const h = date.getHours();
    const m = date.getMinutes().toString().padStart(2, '0');
    const ampm = h >= 12 ? 'PM' : 'AM';
    const h12 = (h % 12) || 12;
    return `${h12}:${m} ${ampm}`;
  };

  const remainingEl = document.getElementById('remaining');
  const endsAtEl = document.getElementById('endsAt');

  if (Number.isFinite(remaining) && remaining > 0) {
    const endsAt = new Date(Date.now() + remaining * 1000);
    const text = formatTime(endsAt);
    remainingEl.textContent = text;
    endsAtEl.textContent = text;
  } else {
    remainingEl.textContent = 'session end';
    endsAtEl.textContent = 'session end';
  }
})();
