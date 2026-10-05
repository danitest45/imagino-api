// Controlled reference drawn locally. No AI API, private photo, text or network.
const fs = require('node:fs'), path = require('node:path'), zlib = require('node:zlib'), crypto = require('node:crypto');
const size = 1024, rows = Buffer.alloc((size * 3 + 1) * size);
function inside(x,y,l,t,r,b,rad=0) {
  if (x<l||x>r||y<t||y>b) return false;
  const cx=Math.max(l+rad,Math.min(r-rad,x)),cy=Math.max(t+rad,Math.min(b-rad,y));
  return (x-cx)**2+(y-cy)**2<=rad**2;
}
for(let y=0;y<size;y++) for(let x=0;x<size;x++) {
  let c=[239-Math.round(y/100),230-Math.round(y/100),211-Math.round(y/100)];
  if(((x-512)/235)**2+((y-798)/35)**2<1)c=[198,186,161];
  if(inside(x,y,300,790,724,865,10))c=[216,201,174];
  if(inside(x,y,355,340,669,788,54)) {
    const light=Math.max(0,1-Math.abs(x-420)/250);
    c=[Math.round(16+light*34),Math.round(41+light*55),Math.round(117+light*96)];
  }
  if(inside(x,y,380,375,402,738,11))c=[116,158,236];
  if(inside(x,y,428,220,596,309,22))c=[20,47,128];
  if(inside(x,y,443,231,458,292,6))c=[88,124,203];
  if(inside(x,y,441,307,583,344,5))c=[197,155,69];
  if(y>=316&&y<=322&&x>=446&&x<=578)c=[240,216,145];
  if([[493,652],[531,652],[512,685]].some(([cx,cy])=>(x-cx)**2+(y-cy)**2<12**2))c=[230,194,104];
  const p=y*(size*3+1)+1+x*3; c.forEach((v,i)=>rows[p+i]=v);
}
function crc32(b){let c=0xffffffff;for(const n of b){c^=n;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return(c^0xffffffff)>>>0;}
function chunk(n,b){const t=Buffer.from(n),o=Buffer.alloc(b.length+12);o.writeUInt32BE(b.length);t.copy(o,4);b.copy(o,8);o.writeUInt32BE(crc32(Buffer.concat([t,b])),b.length+8);return o;}
const h=Buffer.alloc(13);h.writeUInt32BE(size);h.writeUInt32BE(size,4);h[8]=8;h[9]=2;
const b=Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',h),chunk('IDAT',zlib.deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]);
const target=path.join(__dirname,'../Fixtures/bfl-reference-20261005.png');fs.writeFileSync(target,b);
console.log(JSON.stringify({bytes:b.length,width:size,height:size,sha256:crypto.createHash('sha256').update(b).digest('hex')}));
