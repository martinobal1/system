export async function ensureMapLibs() {
  if (window.L && window.L.esri && window.IsSuasDroneMap) return;
  function loadScript(src) {
    return new Promise(function (resolve, reject) {
      var s = document.createElement('script');
      s.src = src;
      s.async = true;
      s.onload = resolve;
      s.onerror = function () { reject(new Error('Nepodarilo sa nacitat ' + src)); };
      document.head.appendChild(s);
    });
  }
  if (!window.L) {
    await loadScript('https://unpkg.com/leaflet@1.9.4/dist/leaflet.js');
  }
  if (!window.L.esri) {
    await loadScript('https://unpkg.com/esri-leaflet@3.0.12/dist/esri-leaflet.js');
  }
  if (!window.IsSuasDroneMap) {
    await loadScript('/js/dronemap.js');
  }
}
