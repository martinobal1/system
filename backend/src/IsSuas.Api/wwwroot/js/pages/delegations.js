import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { bindFormReveals, chipHtml, confirmAction, content, makeTable, pageHead, sectionCard, showHistory, statusBadge, typeLabelSk } from '../core/ui.js';

export async function renderDelegations() {
      let items = [];
      let accounts = [];
      try {
        items = await api('/api/v1/delegations');
      } catch (err) {
        content.innerHTML = '<div class="error">' + err.message + '</div>';
        return;
      }
      try {
        accounts = await api('/api/v1/accounts');
      } catch (_) { /* optional for create */ }

      const permOpts = [
        'integrations.read', 'reports.write', 'uas.read', 'operations.read',
        'documents.read', 'pilots.read', 'accounts.read'
      ].map(function (p) {
        return '<option value="' + p + '">' + p + '</option>';
      }).join('');
      const accountOpts = accounts.map(function (a) {
        return '<option value="' + a.id + '">' + a.userName +
          ' — ' + (a.firstName || '') + ' ' + (a.lastName || '') + '</option>';
      }).join('') || '<option value="">(ziadne ucty)</option>';

      const rows = items.map(function (d) {
        return '<tr><td>' + d.fromUserName + '</td><td>' + d.toUserName +
          '</td><td>' + typeLabelSk(d.permissionCode) + '</td><td>' + statusBadge(d.isActive ? 'Active' : 'Revoked') +
          '</td><td>' + new Date(d.validFrom).toLocaleString('sk-SK') +
          '</td><td>' + (d.validTo ? new Date(d.validTo).toLocaleString('sk-SK') : '—') +
          '</td><td>' + (d.reason || '') + '</td><td>' +
          (d.isActive
            ? '<button type="button" class="secondary revoke-del" data-id="' + d.id +
              '" data-rv="' + d.rowVersion + '">Zrusit</button>'
            : '—') +
          ' <button type="button" class="secondary hist-del" data-id="' + d.id + '">Historia</button>' +
          '</td></tr>';
      }).join('') || '<tr><td colspan="8">Ziadne aktivne delegacie</td></tr>';

      content.innerHTML =
        '<div class="stack">' +
        pageHead('Delegacie opravneni', 'Dočasné prenosy oprávnení medzi prevádzkovateľmi.',
          chipHtml('Aktívne', items.length, 'chip-soft')) +
        '<div id="del-msg"></div>' +
        '<div class="toolbar">' +
        '<button type="button" data-reveal="del-form">Nova delegacia</button>' +
        '</div>' +
        '<div id="del-form" class="form-panel hidden">' +
        '<h3>Delegovat opravnenie</h3>' +
        '<form id="del-create" class="form-grid">' +
        '<label class="full">Prijemca<select id="del-to" required>' + accountOpts + '</select></label>' +
        '<label>Opravnenie<select id="del-perm" required>' + permOpts + '</select></label>' +
        '<label>Platne do<input id="del-to-date" type="datetime-local" /></label>' +
        '<label class="full">Dovod<textarea id="del-reason" required></textarea></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Vytvorit</button>' +
        '<button type="button" class="secondary" data-hide="del-form">Zrusit</button>' +
        '</div></form></div>' +
        sectionCard('Aktivne / zoznam', makeTable(['Od', 'Komu', 'Opravnenie', 'Stav', 'Od', 'Do', 'Dovod', 'Akcie'], rows, true), '#0d8f7a') +
        '<div id="del-history" class="history-panel hidden"></div>' +
        '</div>';
      bindFormReveals();

      document.getElementById('del-create').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('del-msg');
        try {
          const validToEl = document.getElementById('del-to-date');
          let validTo = null;
          if (validToEl.value) validTo = new Date(validToEl.value).toISOString();
          await api('/api/v1/delegations', {
            method: 'POST',
            body: JSON.stringify({
              toAccountId: document.getElementById('del-to').value,
              permissionCode: document.getElementById('del-perm').value,
              orgUnitId: null,
              validFrom: new Date().toISOString(),
              validTo: validTo,
              reason: document.getElementById('del-reason').value
            })
          });
          msg.innerHTML = '<div class="msg">Delegacia vytvorena.</div>';
          navigate('delegations');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.querySelectorAll('.revoke-del').forEach(function (btn) {
        btn.onclick = async function () {
          if (!confirmAction('Naozaj zrusit tuto delegaciu?')) return;
          const msg = document.getElementById('del-msg');
          try {
            await api('/api/v1/delegations/' + btn.dataset.id + '/revoke', {
              method: 'POST',
              body: JSON.stringify({
                reason: 'Zrusenie z UI',
                rowVersion: Number(btn.dataset.rv)
              })
            });
            msg.innerHTML = '<div class="msg">Delegacia zrusena.</div>';
            navigate('delegations');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });

      document.querySelectorAll('.hist-del').forEach(function (btn) {
        btn.onclick = function () {
          showHistory('DelegatedPermission', btn.dataset.id, document.getElementById('del-history'));
        };
      });
      return;
}
