import { api } from '../core/api.js';
import { state } from '../core/state.js';

// Katalóg je v databáze (/api/v1/uas-types): vlastné typy, skryté vstavané typy a PDF manuály.
// Vstavané typy (napr. Mavic 3T) ostávajú v kóde nižšie.
var serverRows = null;

export async function ensureTechDocCatalog() {
  try {
    serverRows = (await api('/api/v1/uas-types')) || [];
  } catch (_) {
    serverRows = serverRows || null;
    return;
  }
  // Jednorazový presun typov, ktoré gestor kedysi uložil len vo svojom prehliadači.
  if (Number(state.accessLevel) === 1) {
    var extras = loadTechDocExtras();
    var removed = loadTechDocRemoved();
    if (extras.length || removed.length) {
      var known = {};
      serverRows.forEach(function (r) { known[r.slug] = r; });
      try {
        for (var i = 0; i < extras.length; i++) {
          var e = extras[i];
          if (!known[e.id]) await saveTechDocToServer(e);
        }
        for (var j = 0; j < removed.length; j++) {
          if (!known[removed[j]] || !known[removed[j]].isHidden) await hideTechDocOnServer(removed[j]);
        }
        saveTechDocExtras([]);
        saveTechDocRemoved([]);
        serverRows = (await api('/api/v1/uas-types')) || [];
      } catch (_) { /* skúsi sa nabudúce */ }
    }
  }
}

function saveTechDocToServer(doc) {
  return api('/api/v1/uas-types/' + encodeURIComponent(doc.id), {
    method: 'PUT',
    body: JSON.stringify({
      manufacturer: doc.manufacturer,
      model: doc.model,
      data: {
        manufacturer: doc.manufacturer,
        model: doc.model,
        classEu: doc.classEu || '—',
        tagline: doc.tagline || '',
        matchModels: doc.matchModels && doc.matchModels.length ? doc.matchModels : [doc.model],
        sections: doc.sections || []
      }
    })
  });
}

function hideTechDocOnServer(id, doc) {
  return api('/api/v1/uas-types/' + encodeURIComponent(id) + '/hide', {
    method: 'POST',
    body: JSON.stringify({ manufacturer: doc ? doc.manufacturer : null, model: doc ? doc.model : null })
  });
}

export function getTechDocFormSchema() {
  function f(key, label, opts) {
    opts = opts || {};
    return {
      key: key,
      label: label,
      specLabel: opts.specLabel != null ? opts.specLabel : label,
      placeholder: opts.placeholder || '',
      required: !!opts.required
    };
  }
  return [
    {
      title: 'Základné údaje',
      fields: [
        f('manufacturer', 'Výrobca', { required: true, placeholder: 'napr. DJI', specLabel: 'Výrobca' }),
        f('model', 'Model', { required: true, placeholder: 'napr. Mavic 3T', specLabel: 'Model' }),
        f('classEu', 'EÚ trieda', { placeholder: 'napr. C2', specLabel: null }),
        f('tagline', 'Kategória / popis', { placeholder: 'napr. Termovízny dron', specLabel: null }),
        f('weight', 'Hmotnosť', { placeholder: 'napr. 920 g' }),
        f('mtow', 'Maximálna vzletová hmotnosť', { placeholder: 'napr. 1 050 g' }),
        f('dimFolded', 'Rozmery – zložený', { placeholder: 'napr. 221 × 96,3 × 90,3 mm' }),
        f('dimUnfolded', 'Rozmery – rozložený', { placeholder: 'napr. 347,5 × 283 × 107,7 mm' }),
        f('flightTime', 'Maximálny čas letu', { placeholder: 'napr. 45 min' }),
        f('hoverTime', 'Maximálny čas visenia', { placeholder: 'napr. 38 min' }),
        f('maxSpeed', 'Maximálna rýchlosť', { placeholder: 'napr. 21 m/s' }),
        f('windResist', 'Odolnosť proti vetru', { placeholder: 'napr. 12 m/s' }),
        f('maxAltitude', 'Maximálna vzletová nadmorská výška', { placeholder: 'napr. 6 000 m' }),
        f('opTemp', 'Prevádzková teplota', { placeholder: 'napr. −10 až 40 °C' }),
        f('ipRating', 'IP krytie', { placeholder: 'napr. IP54 / Výrobca neuvádza' }),
        f('aircraftType', 'Typ lietadla', { placeholder: 'napr. Viacrotorové s termokamerou' }),
        f('navSystems', 'Navigačné systémy', { placeholder: 'napr. GPS, Galileo, BeiDou' }),
        f('storage', 'Interné úložisko', { placeholder: 'napr. 24 GB' })
      ]
    },
    {
      title: 'Širokouhlá kamera',
      fields: [
        f('wideSensor', 'Snímač', { placeholder: 'napr. 1/2″ CMOS' }),
        f('wideRes', 'Rozlíšenie', { placeholder: 'napr. 48 Mpx' }),
        f('wideFocal', 'Ohnisková vzdialenosť', { placeholder: 'napr. 24 mm' }),
        f('wideAperture', 'Clona', { placeholder: 'napr. f/2,8' }),
        f('wideVideo', 'Video', { placeholder: 'napr. 4K/30 fps' }),
        f('widePhotoFmt', 'Formát fotografií', { placeholder: 'napr. JPEG' })
      ]
    },
    {
      title: 'Telekamera',
      fields: [
        f('teleSensor', 'Snímač', { placeholder: 'napr. 1/2″ CMOS' }),
        f('teleRes', 'Rozlíšenie', { placeholder: 'napr. 12 Mpx' }),
        f('teleFocal', 'Ohnisková vzdialenosť', { placeholder: 'napr. 162 mm' }),
        f('teleDigZoom', 'Digitálny zoom', { placeholder: 'napr. 8×' }),
        f('teleHybridZoom', 'Hybridný zoom', { placeholder: 'napr. 56×' }),
        f('teleVideo', 'Video', { placeholder: 'napr. 4K/30 fps' })
      ]
    },
    {
      title: 'Termokamera',
      fields: [
        f('thermalType', 'Typ snímača', { placeholder: 'napr. Nechladený VOx / FLIR Lepton' }),
        f('thermalRes', 'Rozlíšenie', { placeholder: 'napr. 640 × 512 px' }),
        f('thermalHz', 'Obnovovacia frekvencia', { placeholder: 'napr. 30 Hz' }),
        f('thermalZoom', 'Digitálny zoom', { placeholder: 'napr. 28×' }),
        f('thermalMeasure', 'Meranie teploty', { placeholder: 'napr. Bodové a plošné' }),
        f('thermalHighGain', 'Vysoké zosilnenie', { placeholder: 'napr. −20 až 150 °C' }),
        f('thermalLowGain', 'Nízke zosilnenie', { placeholder: 'napr. 0 až 500 °C' }),
        f('thermalSpectrum', 'Spektrálny rozsah', { placeholder: 'napr. 8 až 14 μm' }),
        f('thermalAlarm', 'Teplotný alarm', { placeholder: 'napr. Nastaviteľné upozornenie' }),
        f('thermalModes', 'Režimy zobrazenia', { placeholder: 'napr. Vizuálny, termálny, kombinovaný' })
      ]
    },
    {
      title: 'Navigácia a prenos',
      fields: [
        f('gnss', 'GNSS', { placeholder: 'napr. GPS, Galileo, BeiDou' }),
        f('glonass', 'GLONASS', { placeholder: 'napr. S RTK modulom' }),
        f('rtk', 'RTK', { placeholder: 'napr. Voliteľný modul' }),
        f('linkSystem', 'Prenosový systém', { placeholder: 'napr. DJI O3 Enterprise' }),
        f('linkBands', 'Frekvenčné pásma', { placeholder: 'napr. 2,4 GHz a 5,8 GHz' }),
        f('linkRange', 'Maximálny dosah CE', { placeholder: 'napr. 8 km' }),
        f('liveView', 'Živý obraz', { placeholder: 'napr. 1080p/30 fps' }),
        f('obstacle', 'Detekcia prekážok', { placeholder: 'napr. Všesmerový systém' }),
        f('appName', 'Ovládacia aplikácia', { placeholder: 'napr. DJI Pilot' })
      ]
    },
    {
      title: 'Batéria',
      fields: [
        f('batType', 'Typ', { placeholder: 'napr. LiPo 4S' }),
        f('batCapacity', 'Kapacita', { placeholder: 'napr. 5 000 mAh' }),
        f('batEnergy', 'Energia', { placeholder: 'napr. 77 Wh' }),
        f('batVoltage', 'Napätie', { placeholder: 'napr. 15,4 V' }),
        f('batWeight', 'Hmotnosť batérie', { placeholder: 'napr. 297 g' }),
        f('batChargeTemp', 'Teplota nabíjania', { placeholder: 'napr. 5 až 40 °C' }),
        f('batChargePower', 'Výkon nabíjačky', { placeholder: 'napr. 100 W' }),
        f('batSelfHeat', 'Samoohrev', { placeholder: 'napr. Áno / Nie' })
      ]
    },
    {
      title: 'Bezpečnosť a vybavenie',
      fields: [
        f('safetySensors', 'Snímanie prekážok', { placeholder: 'napr. Predné, zadné, bočné, spodné' }),
        f('safetyIr', 'Infračervené senzory', { placeholder: 'napr. Horné a spodné' }),
        f('safetyRth', 'Návrat', { placeholder: 'napr. Automatický návrat na miesto vzletu' }),
        f('safetyEmergency', 'Núdzové funkcie', { placeholder: 'napr. Núdzové zastavenie a visenie' }),
        f('safetyAccessories', 'Prídavné zariadenia', { placeholder: 'napr. Reflektor, reproduktor, maják' }),
        f('safetyData', 'Bezpečnosť údajov', { placeholder: 'napr. Heslo, časová pečiatka, GPS' })
      ]
    }
  ];
}

export function techDocSlug(manufacturer, model) {
  return String(manufacturer + '-' + model)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '') || ('custom-' + Date.now());
}

export function loadTechDocExtras() {
  try {
    var raw = localStorage.getItem('issuas.techDoc.extras');
    var list = raw ? JSON.parse(raw) : [];
    return Array.isArray(list) ? list : [];
  } catch (_) {
    return [];
  }
}

export function saveTechDocExtras(list) {
  localStorage.setItem('issuas.techDoc.extras', JSON.stringify(list || []));
}

export function loadTechDocRemoved() {
  try {
    var raw = localStorage.getItem('issuas.techDoc.removed');
    var list = raw ? JSON.parse(raw) : [];
    return Array.isArray(list) ? list : [];
  } catch (_) {
    return [];
  }
}

export function saveTechDocRemoved(list) {
  localStorage.setItem('issuas.techDoc.removed', JSON.stringify(list || []));
}

export async function addTechDocToCatalog(doc) {
  var id = techDocSlug(doc.manufacturer, doc.model);
  var saved = { id: id, manufacturer: doc.manufacturer, model: doc.model, classEu: doc.classEu || '—', tagline: doc.tagline || '',
    matchModels: doc.matchModels || [doc.model], sections: doc.sections || [] };
  if (serverRows !== null) {
    await saveTechDocToServer(saved);
    await ensureTechDocCatalog();
    return id;
  }
  // Záloha, keď server nie je dostupný: len v tomto prehliadači.
  var extras = loadTechDocExtras().filter(function (d) { return d.id !== id; });
  extras.push({
    id: id,
    manufacturer: doc.manufacturer,
    model: doc.model,
    classEu: doc.classEu || '—',
    tagline: doc.tagline || 'Vlastný typ',
    matchModels: doc.matchModels || [doc.model],
    sections: doc.sections || []
  });
  saveTechDocExtras(extras);
  saveTechDocRemoved(loadTechDocRemoved().filter(function (x) { return x !== id; }));
  return id;
}

export async function removeTechDocFromCatalog(id) {
  if (serverRows !== null) {
    var doc = getTechDocCatalog().find(function (d) { return d.id === id; });
    await hideTechDocOnServer(id, doc);
    await ensureTechDocCatalog();
    return;
  }
  saveTechDocExtras(loadTechDocExtras().filter(function (d) { return d.id !== id; }));
  var removed = loadTechDocRemoved();
  if (removed.indexOf(id) < 0) {
    removed.push(id);
    saveTechDocRemoved(removed);
  }
}

export function getTechDocBuiltInCatalog() {
  return [

    {
      id: 'mavic-3t',
      manufacturer: 'DJI',
      model: 'Mavic 3T',
      classEu: 'C2',
      tagline: 'Termovízny dron',
      matchModels: ['Mavic 3T'],
      sections: [
        {
          title: 'Lietadlo',
          rows: [
            ['Výrobca', 'DJI'],
            ['Model', 'Mavic 3T'],
            ['Hmotnosť', '920 g'],
            ['Maximálna vzletová hmotnosť', '1 050 g'],
            ['Rozmery – zložený', '221 × 96,3 × 90,3 mm'],
            ['Rozmery – rozložený', '347,5 × 283 × 107,7 mm'],
            ['Maximálny čas letu', '45 min'],
            ['Maximálny čas visenia', '38 min'],
            ['Maximálna rýchlosť', '21 m/s'],
            ['Odolnosť proti vetru', '12 m/s'],
            ['Maximálna vzletová nadmorská výška', '6 000 m'],
            ['Prevádzková teplota', '−10 až 40 °C'],
            ['EÚ trieda', 'C2'],
            ['IP krytie', 'Výrobca neuvádza']
          ]
        },
        {
          title: 'Širokouhlá kamera',
          rows: [
            ['Snímač', '1/2″ CMOS'],
            ['Rozlíšenie', '48 Mpx'],
            ['Ohnisková vzdialenosť', '24 mm'],
            ['Clona', 'f/2,8'],
            ['Video', '4K/30 fps'],
            ['Formát fotografií', 'JPEG']
          ]
        },
        {
          title: 'Telekamera',
          rows: [
            ['Snímač', '1/2″ CMOS'],
            ['Rozlíšenie', '12 Mpx'],
            ['Ohnisková vzdialenosť', '162 mm'],
            ['Digitálny zoom', '8×'],
            ['Hybridný zoom', '56×'],
            ['Video', '4K/30 fps']
          ]
        },
        {
          title: 'Termokamera',
          rows: [
            ['Typ snímača', 'Nechladený VOx mikrobolometer'],
            ['Rozlíšenie', '640 × 512 px'],
            ['Obnovovacia frekvencia', '30 Hz'],
            ['Digitálny zoom', '28×'],
            ['Meranie teploty', 'Bodové a plošné'],
            ['Vysoké zosilnenie', '−20 až 150 °C'],
            ['Nízke zosilnenie', '0 až 500 °C'],
            ['Spektrálny rozsah', '8 až 14 μm']
          ]
        },
        {
          title: 'Navigácia a prenos',
          rows: [
            ['GNSS', 'GPS, Galileo, BeiDou'],
            ['GLONASS', 'S RTK modulom'],
            ['RTK', 'Voliteľný modul'],
            ['Prenosový systém', 'DJI O3 Enterprise'],
            ['Maximálny dosah CE', '8 km'],
            ['Živý obraz', '1080p/30 fps'],
            ['Detekcia prekážok', 'Všesmerový kamerový systém a spodný IR senzor']
          ]
        },
        {
          title: 'Batéria',
          rows: [
            ['Typ', 'LiPo 4S'],
            ['Kapacita', '5 000 mAh'],
            ['Energia', '77 Wh'],
            ['Napätie', '15,4 V'],
            ['Teplota nabíjania', '5 až 40 °C'],
            ['Výkon nabíjačky', '100 W']
          ]
        }
      ]
    },
    {
      id: 'mavic-2-enterprise-dual',
      manufacturer: 'DJI',
      model: 'Mavic 2 Enterprise Dual',
      classEu: '—',
      tagline: 'Termovízny enterprise dron',
      matchModels: ['Mavic 2 Enterprise Dual'],
      sections: [
        {
          title: 'Základné údaje',
          rows: [
            ['Typ', 'Viacrotorové bezpilotné lietadlo s termokamerou'],
            ['Hmotnosť pri vzlete', '899 g'],
            ['Maximálna vzletová hmotnosť', '1 100 g'],
            ['Rozmery v zloženom stave', '214 × 91 × 84 mm'],
            ['Rozmery v rozloženom stave', '322 × 242 × 84 mm'],
            ['Maximálny čas letu', '31 minút'],
            ['Maximálny čas visenia', '29 minút bez vetra'],
            ['Maximálna rýchlosť', '72 km/h'],
            ['Odolnosť proti vetru', '29 – 38 km/h'],
            ['Maximálna prevádzková výška', '6 000 m nad morom'],
            ['Prevádzková teplota', '−10 až 40 °C'],
            ['Navigačné systémy', 'GPS + GLONASS'],
            ['Interné úložisko', '24 GB'],
            ['Stupeň krytia dronu', 'Výrobca neuvádza IP hodnotenie']
          ]
        },
        {
          title: 'Kamerový systém',
          rows: [
            ['Konštrukcia kamery', 'Integrovaná vizuálna a termálna kamera'],
            ['Stabilizácia', 'Trojosový mechanický gimbal'],
            ['Vizuálny snímač', '1/2,3″ CMOS'],
            ['Rozlíšenie fotografií', '12 Mpx'],
            ['Maximálne rozlíšenie videa', '4K – 3840 × 2160 px'],
            ['Tepelný snímač', 'FLIR Lepton'],
            ['Rozlíšenie termálneho obrazu', '160 × 120 px'],
            ['Meranie teploty', 'Bodové meranie a meranie vybranej oblasti'],
            ['Teplotný alarm', 'Nastaviteľné upozornenie na vysokú teplotu'],
            ['Režimy zobrazenia', 'Vizuálny, termálny a kombinovaný obraz'],
            ['Optický zoom', 'Nie'],
            ['Výmena kamery', 'Kamera nie je používateľsky odnímateľná']
          ]
        },
        {
          title: 'Prenos a riadenie',
          rows: [
            ['Prenosový systém', 'OcuSync 2.0'],
            ['Frekvenčné pásma', '2,400 – 2,4835 GHz a 5,725 – 5,850 GHz'],
            ['Maximálny dosah podľa CE', '6 km, bez prekážok a rušenia'],
            ['Ovládacia aplikácia', 'DJI Pilot'],
            ['Živý náhľad', 'Do 1080p pri 30 fps'],
            ['Bezpečnostné funkcie', 'Ochrana interných údajov heslom, časová pečiatka a GPS údaje']
          ]
        },
        {
          title: 'Batéria',
          rows: [
            ['Typ', 'Inteligentná LiPo 4S batéria'],
            ['Kapacita', '3 850 mAh'],
            ['Energia', '59,29 Wh'],
            ['Napätie', '15,4 V'],
            ['Hmotnosť', '297 g'],
            ['Teplota nabíjania', '5 až 40 °C'],
            ['Maximálny nabíjací výkon', '80 W'],
            ['Samoohrev', 'Áno']
          ]
        },
        {
          title: 'Bezpečnosť a vybavenie',
          rows: [
            ['Snímanie prekážok', 'Viacsmerové — predné, zadné, bočné a spodné kamerové snímače'],
            ['Infračervené senzory', 'Horné a spodné'],
            ['Návrat', 'Automatický návrat na miesto vzletu'],
            ['Núdzové funkcie', 'Núdzové zastavenie a visenie'],
            ['Prídavné zariadenia', 'Podpora reflektora, reproduktora alebo výstražného majáka'],
            ['Súčasné príslušenstvo', 'Súčasne možno používať iba jedno prídavné zariadenie']
          ]
        }
      ]
    }
  ];
}

export function getTechDocCatalog() {
  if (serverRows !== null) return catalogFromServer();
  var removed = loadTechDocRemoved();
  var builtIn = getTechDocBuiltInCatalog().filter(function (d) {
    return removed.indexOf(d.id) < 0;
  });
  var extras = loadTechDocExtras().filter(function (d) {
    return removed.indexOf(d.id) < 0
      && !builtIn.some(function (b) { return b.id === d.id; });
  });
  return builtIn.concat(extras);
}

function catalogFromServer() {
  var bySlug = {};
  serverRows.forEach(function (r) { bySlug[r.slug] = r; });
  function withManual(doc, row) {
    if (row && row.hasManual) {
      doc.hasManual = true;
      doc.manualFileName = row.manualFileName;
      doc.manualSizeBytes = row.manualSizeBytes;
      doc.manualUploadedAt = row.manualUploadedAt;
    }
    return doc;
  }
  function fromRow(row, base) {
    var d = row.data || {};
    var hasSections = Array.isArray(d.sections) && d.sections.length > 0;
    var doc = Object.assign({}, base || {}, {
      id: row.slug,
      manufacturer: d.manufacturer || row.manufacturer || (base && base.manufacturer) || '',
      model: d.model || row.model || (base && base.model) || ''
    });
    if (hasSections || !base) {
      doc.classEu = d.classEu || (base && base.classEu) || '—';
      doc.tagline = d.tagline != null ? d.tagline : (base && base.tagline) || '';
      doc.matchModels = Array.isArray(d.matchModels) && d.matchModels.length ? d.matchModels : [doc.model];
      doc.sections = hasSections ? d.sections : ((base && base.sections) || []);
    }
    return withManual(doc, row);
  }
  var builtIn = getTechDocBuiltInCatalog()
    .filter(function (d) { return !(bySlug[d.id] && bySlug[d.id].isHidden); })
    .map(function (d) { return bySlug[d.id] ? fromRow(bySlug[d.id], d) : d; });
  var builtInIds = {};
  builtIn.forEach(function (d) { builtInIds[d.id] = true; });
  getTechDocBuiltInCatalog().forEach(function (d) { builtInIds[d.id] = true; });
  var custom = serverRows
    .filter(function (r) { return !r.isHidden && !builtInIds[r.slug] && r.data && Array.isArray(r.data.sections); })
    .map(function (r) { return fromRow(r, null); });
  return builtIn.concat(custom);
}
