(function(global){
  'use strict';
  /* Conjunto único de ícones da interface: traço 1.8, cantos arredondados, 24×24.
     Glifos Unicode variam por fonte/sistema; SVG mantém peso e alinhamento iguais. */
  var P={
    city:'<path d="M3 21h18"/><path d="M5 21V9l5-3v15"/><path d="M10 21V4l9 4v13"/><path d="M13 10h3M13 14h3M13 18h3"/>',
    notes:'<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h4"/>',
    sparkle:'<path d="M12 3.5l1.9 5 5.1 1.9-5.1 1.9-1.9 5-1.9-5L5 10.4l5.1-1.9z"/><path d="M18.5 15.5l.7 1.8 1.8.7-1.8.7-.7 1.8-.7-1.8-1.8-.7 1.8-.7z"/>',
    plus:'<path d="M12 5v14M5 12h14"/>',
    brush:'<path d="M18.4 3.6a2 2 0 0 1 2.9 2.9l-8.8 8.8-2.9-2.9z"/><path d="M9.6 12.4c-2 0-3.6 1.6-3.6 3.6 0 1.4-1 2.5-2.5 2.5 1 1.6 2.8 2.5 4.6 2.5 2.8 0 5-2.2 5-5z"/>',
    puzzle:'<path d="M9 4.5a2 2 0 1 1 4 0V6h4a1 1 0 0 1 1 1v4h-1.5a2 2 0 1 0 0 4H18v4a1 1 0 0 1-1 1h-4v-1.5a2 2 0 1 0-4 0V20H5a1 1 0 0 1-1-1v-4h1.5a2 2 0 1 0 0-4H4V7a1 1 0 0 1 1-1h4z"/>',
    image:'<rect x="3.5" y="4.5" width="17" height="15" rx="2.5"/><circle cx="9" cy="10" r="1.6"/><path d="M20.5 16l-5-5-8.5 8.5"/>',
    sun:'<circle cx="12" cy="12" r="4"/><path d="M12 2.5v2M12 19.5v2M4.6 4.6 6 6M18 18l1.4 1.4M2.5 12h2M19.5 12h2M4.6 19.4 6 18M18 6l1.4-1.4"/>',
    moon:'<path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z"/>',
    book:'<path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5z"/><path d="M4 20.5A2.5 2.5 0 0 0 6.5 23H20v-5"/><path d="M8.5 7.5h7M8.5 11h5"/>',
    type:'<path d="M5 7V5h14v2M12 5v14M9 19h6"/>',
    shield:'<path d="M12 3 4.5 6v5.5c0 4.6 3.2 8.4 7.5 9.5 4.3-1.1 7.5-4.9 7.5-9.5V6z"/><path d="m9 12 2 2 4-4"/>',
    grid:'<rect x="3.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="3.5" y="13.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="13.5" width="7" height="7" rx="1.5"/>',
    pencil:'<path d="M4 20l1-4L16.5 4.5a2.1 2.1 0 0 1 3 3L8 19z"/><path d="M14.5 6.5l3 3"/>',
    bucket:'<path d="M5 11l7-7 7 7-7 7z"/><path d="M19.5 14.5s1.5 2 1.5 3a1.5 1.5 0 0 1-3 0c0-1 1.5-3 1.5-3z"/>',
    dropper:'<path d="M14.5 4.5l5 5M17 7l-9.5 9.5L5 19l2.5-2.5L17 7z"/><path d="M13 3l8 8"/>',
    math:'<path d="M17 5H7l6 7-6 7h10"/>',
    page:'<rect x="3.5" y="3.5" width="17" height="17" rx="3"/><path d="M3.5 9h17M9 20.5V9"/>',
    palette:'<path d="M12 3a9 9 0 1 0 0 18c1.1 0 1.7-.8 1.7-1.7 0-.5-.2-.9-.5-1.2-.3-.3-.5-.7-.5-1.2 0-.9.8-1.7 1.7-1.7h2A4.6 4.6 0 0 0 21 10.6C21 6.4 17 3 12 3z"/><circle cx="7.5" cy="11" r="1.2"/><circle cx="10" cy="7" r="1.2"/><circle cx="15" cy="7" r="1.2"/>',
    search:'<circle cx="11" cy="11" r="6.5"/><path d="M20 20l-4.2-4.2"/>',
    settings:'<path d="M4 7h9M17 7h3M4 17h3M11 17h9"/><circle cx="15" cy="7" r="2"/><circle cx="9" cy="17" r="2"/>',
    map:'<path d="M9 4.5L3.5 6.5v13l5.5-2 6 2 5.5-2v-13l-5.5 2z"/><path d="M9 4.5v13M15 6.5v13"/>',
    more:'<circle cx="5.5" cy="12" r="1.3"/><circle cx="12" cy="12" r="1.3"/><circle cx="18.5" cy="12" r="1.3"/>',
    back:'<path d="M15 18l-6-6 6-6"/>',
    close:'<path d="M6.5 6.5l11 11M17.5 6.5l-11 11"/>',
    folder:'<path d="M3.5 7.5a2 2 0 0 1 2-2h3.8l2 2h7.2a2 2 0 0 1 2 2v7.5a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z"/>',
    file:'<path d="M14 3.5H7.5a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h9a2 2 0 0 0 2-2V8z"/><path d="M14 3.5V8h4.5"/>',
    chevron:'<path d="M9.5 6l6 6-6 6"/>',
    trash:'<path d="M4.5 7h15"/><path d="M9.5 7V4.5h5V7"/><path d="M6.5 7l.9 12.5h9.2L17.5 7"/><path d="M10.2 11v5.5M13.8 11v5.5"/>',
    clock:'<circle cx="12" cy="12" r="8"/><path d="M12 7.5V12l3 2"/>',
    star:'<path d="M12 4l2.4 5 5.3.6-3.9 3.6 1.1 5.3L12 15.9l-4.9 2.6 1.1-5.3-3.9-3.6 5.3-.6z"/>',
    layers:'<path d="M12 3.5l8.5 4.5-8.5 4.5L3.5 8z"/><path d="M3.5 12.5l8.5 4.5 8.5-4.5"/>',
    download:'<path d="M12 4.5v11M7.5 11l4.5 4.5 4.5-4.5"/><path d="M4.5 15v3a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3"/>',
    upload:'<path d="M12 15.5V4.5M7.5 9l4.5-4.5L16.5 9"/><path d="M4.5 15v3a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3"/>',
    edit:'<path d="M4.5 19.5h4l10-10-4-4-10 10z"/><path d="M13 7l4 4"/>',
    house:'<path d="M4 11l8-6.5 8 6.5"/><path d="M6 9.5v10h12v-10"/><path d="M10 19.5v-5h4v5"/>',
    region:'<rect x="4" y="4" width="16" height="16" rx="2" stroke-dasharray="3 2.6"/>',
    check:'<path d="M5 12.5l4.5 4.5L19 7.5"/>',
    select:'<rect x="4" y="4" width="16" height="16" rx="3"/><path d="M8.5 12l2.5 2.5 4.5-5"/>',
    storage:'<ellipse cx="12" cy="6.5" rx="7" ry="2.8"/><path d="M5 6.5v11c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8v-11"/><path d="M5 12c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8"/>',
    copy:'<rect x="8.5" y="8.5" width="11" height="11" rx="2"/><path d="M15.5 8.5V6.5a2 2 0 0 0-2-2h-7a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2"/>',
    move:'<path d="M4.5 12h15M15 7.5l4.5 4.5-4.5 4.5"/>',
    restore:'<path d="M4.5 12a7.5 7.5 0 1 0 2.2-5.3L4.5 9"/><path d="M4.5 4.5V9H9"/>',
    open:'<path d="M14 4.5h5.5V10"/><path d="M19.5 4.5L11 13"/><path d="M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4"/>',
    sidebar:'<rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M15 4.5v15"/>',
    link:'<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>',
    wikilink:'<path d="M7.5 5H5v14h2.5M10.5 5H8.5v14h2M13.5 5h2v14h-2M16.5 5H19v14h-2.5"/>',
    list:'<path d="M9.5 6.5h10M9.5 12h10M9.5 17.5h10"/><circle cx="5" cy="6.5" r=".9"/><circle cx="5" cy="12" r=".9"/><circle cx="5" cy="17.5" r=".9"/>',
    task:'<rect x="4" y="4.5" width="15" height="15" rx="3"/><path d="M8 12l2.8 2.8L16 9.5"/>',
    quote:'<path d="M5 8.5h5v5.5c0 2.5-1.5 4-4 4.5M14 8.5h5v5.5c0 2.5-1.5 4-4 4.5"/>',
    code:'<path d="M9 7.5L4.5 12 9 16.5M15 7.5l4.5 4.5-4.5 4.5"/>',
    rule:'<path d="M4 12h16"/>',
    arrowUp:'<path d="M12 19V5M5.5 11.5L12 5l6.5 6.5"/>',
    enter:'<path d="M19 5.5V11a3 3 0 0 1-3 3H5"/><path d="M9 10l-4 4 4 4"/>'
  };
  function icon(name,cls){
    var body=P[name]||P.file;
    return '<svg class="ui-icon'+(cls?' '+cls:'')+'" viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">'+body+'</svg>';
  }
  /* glifos antigos dos menus → ícone equivalente */
  var GLYPHS={'Aa':'edit','✎':'edit','←':'back','↪':'move','⇧':'download','⇩':'upload','⌂':'house','▣':'folder','📁':'folder','📄':'file','◉':'open','◰':'region','✦':'sparkle','＋':'plus','+':'plus','🗑':'trash','★':'star','↶':'restore','⧉':'copy','☷':'list'};
  function fromGlyph(g){var n=GLYPHS[String(g||'').trim()];return n?icon(n):null}
  /* elementos estáticos marcados com data-icon recebem o SVG */
  function hydrate(root){(root||global.document).querySelectorAll('[data-icon]:empty').forEach(function(el){el.innerHTML=icon(el.getAttribute('data-icon'))})}
  global.UrbeIcons={icon:icon,fromGlyph:fromGlyph,hydrate:hydrate,names:Object.keys(P)};
  if(global.document){if(global.document.readyState==='loading')global.document.addEventListener('DOMContentLoaded',function(){hydrate()});else hydrate()}
})(window);
