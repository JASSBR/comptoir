// The demo scales to zero: while it starts, Azure's proxy answers with a bare error page. This page stays up (static,
// on Vercel), asks the facade for an image until it gets one — the request itself wakes it — then sends the visitor on,
// to the same path on the app's host.
const APP = 'https://app.comptoir.jassbr.me';
const PROBE_EVERY_MS = 2000;
const EXPECTED_MS = 30000;
const SLOW_AFTER_MS = 90000;

const target = APP + location.pathname + location.search + location.hash;
const started = Date.now();
const bar = document.getElementById('bar');
const progress = bar.parentElement;

function step(id, state) {
  document.getElementById(id).className = state;
}

// Eases towards 95 % over the expected wake time, never reaching the end before the app answers.
function tick() {
  const elapsed = Date.now() - started;
  const percent = Math.round(95 * (1 - Math.exp((-2.5 * elapsed) / EXPECTED_MS)));
  bar.style.width = percent + '%';
  progress.setAttribute('aria-valuenow', String(percent));
  if (elapsed > EXPECTED_MS * 0.4) {
    step('step-wake', 'done');
    step('step-legacy', 'active');
  }
  if (elapsed > SLOW_AFTER_MS) {
    document.getElementById('slow').hidden = false;
  }
}

function open() {
  clearInterval(timer);
  document.body.classList.add('ready');
  bar.style.width = '100%';
  step('step-wake', 'done');
  step('step-legacy', 'done');
  step('step-ready', 'active');
  document.getElementById('title').textContent = 'C’est ouvert !';
  location.replace(target);
}

function probe() {
  const image = new Image();
  image.onload = open;
  image.onerror = () => setTimeout(probe, PROBE_EVERY_MS);
  image.src = `${APP}/app/favicon.svg?wake=${Date.now()}`;
}

document.getElementById('retry').href = target;
const timer = setInterval(tick, 250);
probe();
