// Očko „zobraziť / skryť heslo“ pri každom poli s heslom (aj pri dynamicky vložených formulároch).
const EYE = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></svg>';
const EYE_OFF = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 3l18 18"/><path d="M10.6 5.1A10.5 10.5 0 0 1 12 5c6.5 0 10 7 10 7a17.6 17.6 0 0 1-3.2 4.2M6.6 6.6C3.8 8.4 2 12 2 12s3.5 7 10 7c1.8 0 3.4-.5 4.8-1.3"/><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg>';

function setVisible(input, btn, visible) {
  input.type = visible ? 'text' : 'password';
  btn.innerHTML = visible ? EYE_OFF : EYE;
  btn.setAttribute('aria-label', visible ? 'Skryť heslo' : 'Zobraziť heslo');
  btn.title = visible ? 'Skryť heslo' : 'Zobraziť heslo';
  btn.setAttribute('aria-pressed', String(visible));
}

function enhance(input) {
  if (input.dataset.pwToggle) return;
  input.dataset.pwToggle = '1';
  const wrap = document.createElement('span');
  wrap.className = 'pw-field';
  input.parentNode.insertBefore(wrap, input);
  wrap.appendChild(input);
  const btn = document.createElement('button');
  btn.type = 'button';
  btn.className = 'pw-eye';
  btn.tabIndex = -1;
  setVisible(input, btn, false);
  btn.addEventListener('mousedown', function (e) { e.preventDefault(); }); // nestráca fokus z poľa
  btn.addEventListener('click', function () {
    setVisible(input, btn, input.type === 'password');
    input.focus();
  });
  wrap.appendChild(btn);
  // po odoslaní / resete formulára heslo znova skry
  const form = input.form;
  if (form && !form.dataset.pwToggleReset) {
    form.dataset.pwToggleReset = '1';
    const hideAll = function () {
      form.querySelectorAll('input[data-pw-toggle]').forEach(function (i) {
        const b = i.parentNode.querySelector('.pw-eye');
        if (b) setVisible(i, b, false);
      });
    };
    form.addEventListener('submit', hideAll);
    form.addEventListener('reset', hideAll);
  }
}

function scan(root) {
  if (!root || !root.querySelectorAll) return;
  if (root.matches && root.matches('input[type="password"]')) enhance(root);
  root.querySelectorAll('input[type="password"]').forEach(enhance);
}

export function initPasswordToggles() {
  scan(document.body);
  new MutationObserver(function (mutations) {
    mutations.forEach(function (m) { m.addedNodes.forEach(function (n) { if (n.nodeType === 1) scan(n); }); });
  }).observe(document.body, { childList: true, subtree: true });
}
