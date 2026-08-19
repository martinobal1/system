import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { bindFormReveals, chipHtml, content, makeTable, pageHead, statusBadge, typeLabelSk } from '../core/ui.js';

export async function renderAdmin() {
      const codes = await api('/api/v1/lookups');
      const rules = await api('/api/v1/config-rules');
      const connectors = await api('/api/v1/integrations');
      const audit = await api('/api/v1/audit-events');
      let info = null;
      try { info = await api('/api/v1/system/info'); } catch (_) {}

      content.innerHTML =
        '<div class="stack">' +
        pageHead('Administracia', 'Číselníky, konfigurácia, integrácie a audit.',
          chipHtml('Číselníky', codes.length, 'chip-soft') +
          chipHtml('Pravidlá', rules.length) +
          chipHtml('Integrácie', connectors.length)) +
        (info ? '<div class="msg">Verzia ' + info.appVersion + ' · ' + info.environmentName +
          ' · DB ' + info.databaseMode + ' · aktivne pravidla: ' + info.activeConfigRules + '</div>' : '') +
        '<div class="toolbar">' +
        '<button type="button" data-reveal="adm-lookup">Novy ciselnik / polozka</button>' +
        '<button type="button" data-reveal="adm-config">Nove config pravidlo</button>' +
        '<button type="button" data-reveal="adm-int">Integracny konektor</button>' +
        '<button type="button" class="secondary" id="adm-audit-export">Export auditu CSV</button>' +
        '<button type="button" class="secondary" data-reveal="adm-reauth">Opätovné overenie (reauth)</button>' +
        '</div>' +
        '<div id="adm-msg"></div>' +
        '<div id="adm-reauth" class="form-panel hidden">' +
        '<h3>Re-auth (UI-002 light)</h3>' +
        '<form id="reauth-form" class="form-grid">' +
        '<label class="full">Heslo aktualneho ucta<input id="reauth-pw" type="password" required /></label>' +
        '<div class="full toolbar"><button type="submit">Overit</button>' +
        '<button type="button" class="secondary" data-hide="adm-reauth">Zrusit</button></div></form></div>' +
        '<div id="adm-lookup" class="form-panel hidden">' +
        '<h3>Ciselnik</h3>' +
        '<form id="lookup-code-form" class="form-grid">' +
        '<label>Kod<input id="lk-code" required placeholder="UAS_TYPE" /></label>' +
        '<label>Nazov<input id="lk-name" required /></label>' +
        '<div class="full toolbar"><button type="submit">Vytvorit ciselnik</button>' +
        '<button type="button" class="secondary" data-hide="adm-lookup">Zrusit</button></div></form>' +
        '<form id="lookup-item-form" class="form-grid">' +
        '<label>Ciselnik kod<input id="lki-code" required placeholder="UAS_TYPE" /></label>' +
        '<label>Polozka kod<input id="lki-item" required /></label>' +
        '<label class="full">Popis<input id="lki-label" required /></label>' +
        '<div class="full toolbar"><button type="submit">Pridat polozku</button></div></form></div>' +
        '<div id="adm-config" class="form-panel hidden">' +
        '<h3>Konfiguracne pravidlo (nova verzia)</h3>' +
        '<form id="cfg-form" class="form-grid">' +
        '<label>Kluc<input id="cfg-key" required placeholder="uas.registration_number_format" /></label>' +
        '<label class="full">Hodnota JSON<textarea id="cfg-val" required>{"pattern":"SUAS-{YYYY}-{SEQ:5}"}</textarea></label>' +
        '<div class="full toolbar"><button type="submit">Ulozit verziu</button>' +
        '<button type="button" class="secondary" data-hide="adm-config">Zrusit</button></div></form></div>' +
        '<div id="adm-int" class="form-panel hidden">' +
        '<h3>Integracny konektor</h3>' +
        '<form id="int-form" class="form-grid">' +
        '<label>Kod<input id="int-code" required /></label>' +
        '<label>Nazov<input id="int-name" required /></label>' +
        '<label class="full">Koncový bod<input id="int-url" required placeholder="https://..." /></label>' +
        '<label>Odkaz na tajomstvo (nie samotné tajomstvo)<input id="int-secret" placeholder="secrets/xyz" /></label>' +
        '<div class="full toolbar"><button type="submit">Ulozit</button>' +
        '<button type="button" class="secondary" data-hide="adm-int">Zrusit</button></div></form></div>' +
        '<h2>Ciselniky</h2>' +
        makeTable(['Kod', 'Nazov', 'Polozky', 'Aktivny'],
          codes.map(function (c) {
            return '<tr><td>' + c.code + '</td><td>' + c.nameSk + '</td><td>' + c.itemCount +
              '</td><td>' + (c.isActive ? 'Ano' : 'Nie') + '</td></tr>';
          }).join('') || '<tr><td colspan="4">Ziadne</td></tr>') +
        '<h2>Konfiguračné pravidlá</h2>' +
        makeTable(['Kluc', 'Verzia', 'Od', 'Do', 'Hodnota'],
          rules.map(function (r) {
            return '<tr><td>' + r.key + '</td><td>' + r.version + '</td><td>' +
              new Date(r.effectiveFrom).toLocaleString('sk-SK') + '</td><td>' +
              (r.effectiveTo ? new Date(r.effectiveTo).toLocaleString('sk-SK') : '-') +
              '</td><td>' + r.valueJson.substring(0, 40) + '</td></tr>';
          }).join('') || '<tr><td colspan="5">Ziadne</td></tr>') +
        '<h2>Integracie</h2>' +
        makeTable(['Kod', 'Typ', 'Koncovy bod', 'Stav', 'Odkaz na tajomstvo'],
          connectors.map(function (c) {
            return '<tr><td>' + c.code + '</td><td>' + typeLabelSk(c.connectorType) + '</td><td>' + c.endpointUrl +
              '</td><td>' + statusBadge(c.status) + '</td><td>' + (c.secretRef || '-') + '</td></tr>';
          }).join('') || '<tr><td colspan="5">Ziadne</td></tr>') +
        '<h2>Audit (posledne)</h2>' +
        makeTable(['Cas', 'Prevadzkovatel', 'Akcia', 'Objekt', 'Vysledok'],
          audit.slice(0, 30).map(function (a) {
            return '<tr><td>' + new Date(a.occurredAt).toLocaleString('sk-SK') + '</td><td>' +
              a.actorUserName + '</td><td>' + typeLabelSk(a.action) + '</td><td>' + typeLabelSk(a.entityType) +
              '</td><td>' + statusBadge(a.result) + '</td></tr>';
          }).join('') || '<tr><td colspan="5">Ziadne</td></tr>') +
        '</div>';

      bindFormReveals();

      document.getElementById('lookup-code-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('adm-msg');
        try {
          await api('/api/v1/lookups', {
            method: 'POST',
            body: JSON.stringify({
              code: document.getElementById('lk-code').value.trim(),
              nameSk: document.getElementById('lk-name').value.trim(),
              descriptionSk: null
            })
          });
          msg.innerHTML = '<div class="msg">Ciselnik vytvoreny.</div>';
          navigate('admin');
        } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
      };

      document.getElementById('lookup-item-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('adm-msg');
        try {
          const code = document.getElementById('lki-code').value.trim();
          await api('/api/v1/lookups/' + encodeURIComponent(code) + '/items', {
            method: 'POST',
            body: JSON.stringify({
              itemCode: document.getElementById('lki-item').value.trim(),
              labelSk: document.getElementById('lki-label').value.trim(),
              sortOrder: 1,
              metadataJson: null
            })
          });
          msg.innerHTML = '<div class="msg">Polozka pridana.</div>';
          navigate('admin');
        } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
      };

      document.getElementById('cfg-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('adm-msg');
        try {
          await api('/api/v1/config-rules', {
            method: 'POST',
            body: JSON.stringify({
              key: document.getElementById('cfg-key').value.trim(),
              valueJson: document.getElementById('cfg-val').value.trim(),
              effectiveFrom: new Date().toISOString(),
              descriptionSk: 'UI'
            })
          });
          msg.innerHTML = '<div class="msg">Nova verzia pravidla ulozena (stare zaznamy sa nemenia).</div>';
          navigate('admin');
        } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
      };

      document.getElementById('int-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('adm-msg');
        try {
          await api('/api/v1/integrations', {
            method: 'POST',
            body: JSON.stringify({
              code: document.getElementById('int-code').value.trim(),
              nameSk: document.getElementById('int-name').value.trim(),
              connectorType: 'REST',
              endpointUrl: document.getElementById('int-url').value.trim(),
              format: 'JSON',
              scheduleCron: null,
              secretRef: document.getElementById('int-secret').value.trim() || null,
              activate: false
            })
          });
          msg.innerHTML = '<div class="msg">Konektor ulozeny (bez tajomstva v plain texte).</div>';
          navigate('admin');
        } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
      };

      document.getElementById('adm-audit-export').onclick = async function () {
        const msg = document.getElementById('adm-msg');
        try {
          const res = await fetch('/api/v1/audit-events/export.csv', {
            headers: { Authorization: 'Bearer ' + state.token }
          });
          if (!res.ok) throw new Error('Export zlyhal ' + res.status);
          const blob = await res.blob();
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url; a.download = 'audit.csv'; a.click();
          URL.revokeObjectURL(url);
          msg.innerHTML = '<div class="msg">Audit CSV stiahnuty (export je auditovany).</div>';
        } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
      };
      document.getElementById('reauth-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('adm-msg');
        try {
          const res = await api('/api/v1/auth/reauth', {
            method: 'POST',
            body: JSON.stringify({ password: document.getElementById('reauth-pw').value })
          });
          state.reauthToken = res.reauthToken;
          msg.innerHTML = '<div class="msg">Token na opakované overenie vydaný do ' +
            new Date(res.expiresAt).toLocaleString('sk-SK') + '.</div>';
          document.getElementById('adm-reauth').classList.add('hidden');
        } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
      };
      return;
}
