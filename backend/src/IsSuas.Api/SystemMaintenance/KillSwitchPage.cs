namespace IsSuas.Api.SystemMaintenance;

/// <summary>
/// HTML stránky núdzového vypínača. Je zámerne v kóde (nie vo wwwroot), aby sa nedala nájsť
/// ako statický súbor — dostupná je len na tajnej adrese z konfigurácie.
/// Formulár posiela POST na vlastnú adresu (window.location.pathname), takže adresu nepozná ani JavaScript.
/// </summary>
internal static class KillSwitchPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="sk">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <meta name="robots" content="noindex, nofollow" />
  <meta name="referrer" content="no-referrer" />
  <title>Núdzový vypínač – IS ŠUAS</title>
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;650;700&display=swap" rel="stylesheet" />
  <link rel="stylesheet" href="/css/app.css" />
  <link rel="stylesheet" href="/css/theme.css" />
  <style>
    .ks-state { display: flex; align-items: center; gap: 0.6rem; padding: 0.8rem 1rem; border-radius: var(--radius-sm, 9px); border: 1px solid var(--line, #e6eaf0); font-weight: 600; }
    .ks-dot { width: 0.65rem; height: 0.65rem; border-radius: 50%; background: var(--subtle, #94a3b8); flex: none; }
    .ks-state.on { background: #fef2f2; border-color: #fecaca; color: var(--danger, #c0352b); }
    .ks-state.on .ks-dot { background: var(--danger, #c0352b); box-shadow: 0 0 0 4px rgba(192, 53, 43, 0.15); }
    .ks-state.off { background: #f0fdf4; border-color: #bbf7d0; color: var(--ok, #15803d); }
    .ks-state.off .ks-dot { background: var(--ok, #15803d); box-shadow: 0 0 0 4px rgba(21, 128, 61, 0.15); }
    .ks-meta { margin: -0.4rem 0 0; font-size: 0.85rem; }
    .ks-card textarea { width: 100%; box-sizing: border-box; font: inherit; resize: vertical; min-height: 4.5rem; }
    .ks-actions { display: grid; grid-template-columns: 1fr 1fr; gap: 0.6rem; }
    .ks-actions button { width: 100%; }
    button.ks-danger { background: var(--danger, #c0352b); border-color: var(--danger, #c0352b); color: #fff; }
    button.ks-danger:hover { background: #a12a21; border-color: #a12a21; }
    .ks-ok { color: var(--ok, #15803d); margin: 0; font-weight: 600; }
    .ks-warn-list li span { background: rgba(255, 255, 255, 0.12); }
  </style>
</head>
<body>
  <div class="login-shell">
    <aside class="login-hero" aria-hidden="true">
      <div class="login-hero-top">
        <span class="login-hero-logo"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M18.4 5.6a9 9 0 1 1-12.8 0"/><path d="M12 2v10"/></svg></span>
        <span>IS ŠUAS</span>
      </div>
      <div class="login-hero-body">
        <p class="login-eyebrow">Núdzový vypínač</p>
        <h2>Vypnite alebo zapnite celý systém jedným kliknutím.</h2>
        <ul class="login-features ks-warn-list">
          <li><span>!</span>Po vypnutí nefunguje žiadny účet, ani gestor</li>
          <li><span>✓</span>Funguje bez prihlásenia, iba s tajným kľúčom</li>
          <li><span>✓</span>Každý pokus sa zapisuje do auditu</li>
        </ul>
      </div>
      <p class="login-hero-foot">Túto adresu nikomu neposielajte.</p>
    </aside>
    <div class="login-main">
      <form id="ks-form" class="login-card ks-card" autocomplete="off">
        <p class="brand"><span class="brand-mark"><img src="/assets/logominv.png" alt="Ministerstvo vnútra SR" /></span></p>
        <h1>Núdzový vypínač</h1>
        <div id="ks-state" class="ks-state"><span class="ks-dot"></span><span id="ks-state-text">Zisťujem stav…</span></div>
        <p id="ks-meta" class="muted ks-meta hidden"></p>
        <label>Tajný kľúč
          <input id="ks-key" type="password" autocomplete="off" spellcheck="false" placeholder="••••••••••••" required />
        </label>
        <label>Správa pre používateľov (nepovinné)
          <textarea id="ks-msg" maxlength="1000" placeholder="Prebieha plánovaná údržba systému. Skúste to prosím neskôr."></textarea>
        </label>
        <label>Predpokladaný koniec (nepovinné)
          <input id="ks-until" type="datetime-local" />
        </label>
        <p id="ks-error" class="error hidden" role="alert"></p>
        <p id="ks-ok" class="ks-ok hidden" role="status"></p>
        <div class="ks-actions">
          <button type="button" id="ks-off" class="ks-danger">Vypnúť systém</button>
          <button type="button" id="ks-on" class="secondary">Zapnúť systém</button>
        </div>
      </form>
    </div>
  </div>
  <script>
  (function () {
    var stateBox = document.getElementById('ks-state');
    var stateText = document.getElementById('ks-state-text');
    var metaEl = document.getElementById('ks-meta');
    var keyEl = document.getElementById('ks-key');
    var msgEl = document.getElementById('ks-msg');
    var untilEl = document.getElementById('ks-until');
    var errEl = document.getElementById('ks-error');
    var okEl = document.getElementById('ks-ok');
    var offBtn = document.getElementById('ks-off');
    var onBtn = document.getElementById('ks-on');

    function showState(status) {
      if (!status) {
        stateBox.className = 'ks-state';
        stateText.textContent = 'Stav sa nepodarilo zistiť.';
        return;
      }
      stateBox.className = 'ks-state ' + (status.isActive ? 'on' : 'off');
      stateText.textContent = status.isActive
        ? 'Systém je VYPNUTÝ — nikto sa neprihlási.'
        : 'Systém je ZAPNUTÝ a dostupný pre všetkých.';
      var meta = [];
      if (status.isActive && status.plannedEndAt) meta.push('Predpokladaný koniec: ' + new Date(status.plannedEndAt).toLocaleString('sk-SK'));
      if (status.forcedByConfiguration) meta.push('Vynútené konfiguráciou servera (MaintenanceMode__ForceEnabled) — tu sa nedá zapnúť.');
      if (status.changedAt) meta.push('Posledná zmena: ' + new Date(status.changedAt).toLocaleString('sk-SK'));
      metaEl.textContent = meta.join(' · ');
      metaEl.classList.toggle('hidden', meta.length === 0);
    }

    function showError(text) { okEl.classList.add('hidden'); errEl.textContent = text; errEl.classList.remove('hidden'); }
    function showOk(text) { errEl.classList.add('hidden'); okEl.textContent = text; okEl.classList.remove('hidden'); }

    function loadState() {
      fetch('/api/v1/system/maintenance', { cache: 'no-store' })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(showState)
        .catch(function () { showState(null); });
    }

    function send(enabled) {
      var key = keyEl.value.trim();
      if (!key) { showError('Zadajte tajný kľúč.'); keyEl.focus(); return; }
      var question = enabled
        ? 'Naozaj VYPNÚŤ celý systém? Nikto sa neprihlási, ani gestor.'
        : 'Naozaj ZAPNÚŤ systém pre všetkých?';
      if (!window.confirm(question)) return;
      offBtn.disabled = true; onBtn.disabled = true;
      fetch(window.location.pathname, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        cache: 'no-store',
        body: JSON.stringify({
          key: key,
          enabled: enabled,
          message: msgEl.value,
          plannedEndAt: untilEl.value ? new Date(untilEl.value).toISOString() : null
        })
      })
        .then(function (r) {
          return r.json().catch(function () { return null; }).then(function (body) { return { status: r.status, body: body }; });
        })
        .then(function (res) {
          if (res.status === 200) {
            showState(res.body);
            showOk(enabled ? 'Systém bol vypnutý.' : 'Systém bol zapnutý.');
            keyEl.value = '';
          } else if (res.status === 401) {
            showError('Nesprávny kľúč.');
          } else if (res.status === 429) {
            showError('Príliš veľa pokusov. Skúste to znova o minútu.');
          } else {
            showError((res.body && (res.body.detail || res.body.title)) || ('Chyba ' + res.status));
          }
        })
        .catch(function () { showError('Server nie je dostupný.'); })
        .then(function () { offBtn.disabled = false; onBtn.disabled = false; });
    }

    offBtn.addEventListener('click', function () { send(true); });
    onBtn.addEventListener('click', function () { send(false); });
    document.getElementById('ks-form').addEventListener('submit', function (ev) { ev.preventDefault(); });
    loadState();
  })();
  </script>
</body>
</html>
""";
}
