// Original decorative art for the Travian-like look, drawn as SVG so nothing is copied from the game.

const url = (svg: string) => `url("data:image/svg+xml;utf8,${encodeURIComponent(svg.replace(/\s+/g, ' ').trim())}")`

/** Fine paper fibers, tinted to old parchment. Tiles seamlessly. */
export const parchmentTexture = url(`
<svg xmlns='http://www.w3.org/2000/svg' width='300' height='300'>
  <filter id='p'><feTurbulence type='fractalNoise' baseFrequency='.8' numOctaves='3' stitchTiles='stitch'/>
  <feColorMatrix values='0 0 0 0 .45  0 0 0 0 .32  0 0 0 0 .15  0 0 0 .09 0'/></filter>
  <filter id='s'><feTurbulence type='fractalNoise' baseFrequency='.012' numOctaves='2' stitchTiles='stitch'/>
  <feColorMatrix values='0 0 0 0 .55  0 0 0 0 .38  0 0 0 0 .15  0 0 0 .5 -.18'/></filter>
  <rect width='300' height='300' filter='url(#p)'/>
</svg>`)

/** Long wood planks with grain and seams. */
export const woodTexture = url(`
<svg xmlns='http://www.w3.org/2000/svg' width='400' height='240'>
  <filter id='g'><feTurbulence type='fractalNoise' baseFrequency='.004 .09' numOctaves='4' seed='7' stitchTiles='stitch'/>
  <feColorMatrix values='0 0 0 0 .12  0 0 0 0 .07  0 0 0 0 .02  0 0 0 .9 -.25'/></filter>
  <rect width='400' height='240' fill='#5a3a1c'/>
  <rect width='400' height='240' filter='url(#g)'/>
  <g stroke='#2a1808' stroke-opacity='.55' stroke-width='2'>
    <line x1='0' y1='1' x2='400' y2='1'/><line x1='0' y1='81' x2='400' y2='81'/><line x1='0' y1='161' x2='400' y2='161'/>
  </g>
  <g stroke='#c99a5c' stroke-opacity='.18'><line x1='0' y1='3' x2='400' y2='3'/><line x1='0' y1='83' x2='400' y2='83'/><line x1='0' y1='163' x2='400' y2='163'/></g>
  <g fill='#2a1808' fill-opacity='.6'><circle cx='14' cy='40' r='2.5'/><circle cx='386' cy='120' r='2.5'/><circle cx='14' cy='200' r='2.5'/></g>
</svg>`)

/** Gold scroll ornament for the top-left corner of a frame; the others are mirrored with CSS. */
const corner = (transform: string) => url(`
<svg xmlns='http://www.w3.org/2000/svg' width='34' height='34' viewBox='0 0 34 34'>
  <g transform='${transform}' fill='none' stroke-linecap='round'>
    <path d='M3 31 V8 Q3 3 8 3 H31' stroke='#7a5222' stroke-width='3'/>
    <path d='M3 31 V8 Q3 3 8 3 H31' stroke='#e2bf6e' stroke-width='1.2'/>
    <path d='M8 22 Q8 8 22 8' stroke='#b88a3e' stroke-width='1.6'/>
    <circle cx='9' cy='9' r='3.2' fill='#d9ae55' stroke='#7a5222'/>
    <path d='M14 14 q4 -2 6 2 q-4 2 -6 -2z' fill='#b88a3e'/>
  </g>
</svg>`)

export const corners = {
  tl: corner(''),
  tr: corner('translate(34 0) scale(-1 1)'),
  bl: corner('translate(0 34) scale(1 -1)'),
  br: corner('translate(34 34) scale(-1 -1)'),
}

/** Background layers that put an ornament in each corner of a box. */
export const framedCorners = {
  backgroundImage: `${corners.tl}, ${corners.tr}, ${corners.bl}, ${corners.br}`,
  backgroundPosition: '2px 2px, calc(100% - 2px) 2px, 2px calc(100% - 2px), calc(100% - 2px) calc(100% - 2px)',
  backgroundRepeat: 'no-repeat',
}

/** A wide countryside: hills, fields, a palisade village and a watchtower. */
export const landscape = url(`
<svg xmlns='http://www.w3.org/2000/svg' width='1600' height='220' viewBox='0 0 1600 220' preserveAspectRatio='xMidYMax slice'>
  <defs>
    <linearGradient id='sky' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#8fc3e0'/><stop offset='.7' stop-color='#d9ecef'/><stop offset='1' stop-color='#f3ead0'/></linearGradient>
    <linearGradient id='h1' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#9dbb5a'/><stop offset='1' stop-color='#6f9133'/></linearGradient>
    <linearGradient id='h2' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#7ea53c'/><stop offset='1' stop-color='#4f7420'/></linearGradient>
    <linearGradient id='h3' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#5f8a24'/><stop offset='1' stop-color='#3d5c14'/></linearGradient>
    <radialGradient id='sun'><stop offset='0' stop-color='#fff7d0'/><stop offset='.4' stop-color='#ffe69a' stop-opacity='.9'/><stop offset='1' stop-color='#ffe69a' stop-opacity='0'/></radialGradient>
  </defs>
  <rect width='1600' height='220' fill='url(#sky)'/>
  <circle cx='1320' cy='60' r='90' fill='url(#sun)'/>
  <g fill='#fff' fill-opacity='.85'>
    <ellipse cx='220' cy='50' rx='60' ry='14'/><ellipse cx='260' cy='42' rx='40' ry='16'/>
    <ellipse cx='820' cy='35' rx='70' ry='12'/><ellipse cx='860' cy='28' rx='38' ry='13'/>
    <ellipse cx='1500' cy='70' rx='50' ry='10'/>
  </g>
  <path d='M0 120 Q120 70 260 105 T560 95 T880 110 T1200 85 T1600 105 V220 H0z' fill='#a9c2b4' fill-opacity='.7'/>
  <path d='M0 150 Q160 105 340 135 T720 128 T1080 140 T1400 118 T1600 135 V220 H0z' fill='url(#h1)'/>
  <g stroke='#c9b34d' stroke-opacity='.55' stroke-width='3'>
    <path d='M380 160 L520 150 M390 168 L530 158 M400 176 L540 166'/>
    <path d='M1130 158 L1260 150 M1140 166 L1270 158'/>
  </g>
  <path d='M0 185 Q200 150 420 172 T860 168 T1280 178 T1600 160 V220 H0z' fill='url(#h2)'/>
  <g transform='translate(640 128)'>
    <g fill='#6b4423' stroke='#3b2a17' stroke-width='1'>
      ${Array.from({ length: 22 }, (_, i) => `<path d='M${i * 7} 30 V8 l3.5 -5 l3.5 5 V30z'/>`).join('')}
    </g>
    <g transform='translate(30 -14)'>
      <rect x='0' y='14' width='40' height='26' fill='#d8b98a' stroke='#5a3a1c'/>
      <path d='M-6 16 L20 -4 L46 16z' fill='#a8452c' stroke='#5a1f14'/>
      <rect x='16' y='26' width='8' height='14' fill='#5a3a1c'/>
    </g>
    <g transform='translate(88 -6)'>
      <rect x='0' y='10' width='30' height='22' fill='#e2c59a' stroke='#5a3a1c'/>
      <path d='M-5 12 L15 -4 L35 12z' fill='#b85a34' stroke='#5a1f14'/>
    </g>
    <g transform='translate(150 -38)'>
      <rect x='0' y='12' width='16' height='56' fill='#8a6a44' stroke='#3b2a17'/>
      <path d='M-6 14 L8 -6 L22 14z' fill='#7a2e1c' stroke='#3b2a17'/>
      <line x1='8' y1='-6' x2='8' y2='-24' stroke='#3b2a17' stroke-width='1.5'/>
      <path d='M8 -24 l14 4 l-14 4z' fill='#b8281d'/>
    </g>
  </g>
  <g fill='#355a12'>
    <circle cx='90' cy='170' r='16'/><circle cx='112' cy='162' r='20'/><rect x='108' y='175' width='5' height='14' fill='#4a2e15'/>
    <circle cx='1450' cy='150' r='18'/><circle cx='1475' cy='158' r='14'/><rect x='1458' y='162' width='5' height='14' fill='#4a2e15'/>
    <circle cx='1000' cy='166' r='12'/><rect x='998' y='174' width='4' height='10' fill='#4a2e15'/>
  </g>
  <path d='M0 210 Q260 188 560 202 T1100 200 T1600 196 V220 H0z' fill='url(#h3)'/>
</svg>`)

/** The guard crest: a shield with crossed swords. */
export const crest = `data:image/svg+xml;utf8,${encodeURIComponent(`
<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'>
  <defs><linearGradient id='s' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#8fbf2c'/><stop offset='1' stop-color='#3d5e08'/></linearGradient>
  <linearGradient id='b' x1='0' y1='0' x2='1' y2='1'><stop offset='0' stop-color='#f2d48a'/><stop offset='1' stop-color='#9a6a26'/></linearGradient></defs>
  <g stroke='#3b2a17' stroke-width='1.5'>
    <path d='M10 12 L54 52 M54 12 L10 52' stroke='url(#b)' stroke-width='5' stroke-linecap='round'/>
    <path d='M32 6 L52 13 V30 Q52 48 32 58 Q12 48 12 30 V13z' fill='url(#s)'/>
    <path d='M32 10 L48 16 V30 Q48 45 32 53 Q16 45 16 30 V16z' fill='none' stroke='#e2bf6e' stroke-width='1.5'/>
    <path d='M32 18 V44 M24 28 H40' stroke='#f6ead0' stroke-width='3.5' stroke-linecap='round'/>
  </g>
</svg>`.replace(/\s+/g, ' ').trim())}`

/** Thin gold rule with a diamond in the middle, for under titles. */
export const divider = url(`
<svg xmlns='http://www.w3.org/2000/svg' width='240' height='14' viewBox='0 0 240 14'>
  <path d='M2 7 H100 M140 7 H238' stroke='#a07a44' stroke-width='1.5'/>
  <path d='M120 1 L128 7 L120 13 L112 7z' fill='#d9ae55' stroke='#7a5222'/>
  <circle cx='106' cy='7' r='2' fill='#a07a44'/><circle cx='134' cy='7' r='2' fill='#a07a44'/>
</svg>`)
