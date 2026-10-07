// Deterministic synthetic test media. This never invokes an AI service.
const fs = require('node:fs');
const path = require('node:path');
const { deflateSync } = require('node:zlib');
const size = 1024;
const rows = Buffer.alloc((size * 3 + 1) * size);
function pixel(x, y, rgb) { if (x < 0 || y < 0 || x >= size || y >= size) return; const p = y * (size * 3 + 1) + 1 + x * 3; rgb.forEach((v, i) => rows[p + i] = v); }
for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
  const t = (x + y) / (2 * size);
  pixel(x, y, [Math.round(23 + t * 51), Math.round(17 + t * 28), Math.round(49 + t * 80)]);
}
const letters = {
 A:['01110','10001','10001','11111','10001','10001','10001'],
 C:['01111','10000','10000','10000','10000','10000','01111'],
 D:['11110','10001','10001','10001','10001','10001','11110'],
 E:['11111','10000','10000','11110','10000','10000','11111'],
 G:['01111','10000','10000','10111','10001','10001','01111'],
 I:['11111','00100','00100','00100','00100','00100','11111'],
 M:['10001','11011','10101','10101','10001','10001','10001'],
 N:['10001','11001','10101','10011','10001','10001','10001'],
 O:['01110','10001','10001','10001','10001','10001','01110'],
 S:['01111','10000','10000','01110','00001','00001','11110'],
 T:['11111','00100','00100','00100','00100','00100','00100'],
 Y:['10001','10001','01010','00100','00100','00100','00100'],
 H:['10001','10001','10001','11111','10001','10001','10001'],
 ' ':Array(7).fill('00000')
};
function text(value, y, scale, color) {
 let start = Math.floor((size - value.length * 6 * scale) / 2);
 for (const c of value) { letters[c].forEach((row, dy) => [...row].forEach((v, dx) => { if (v === '1') for (let sy=0;sy<scale;sy++) for(let sx=0;sx<scale;sx++) pixel(start+dx*scale+sx,y+dy*scale+sy,color); })); start += 6*scale; }
}
text('IMAGINO', 225, 18, [226,214,255]);
text('STAGING DEMO', 470, 10, [122,225,231]);
text('SYNTHETIC IMAGE', 610, 8, [196,183,224]);
text('NO AI COST', 780, 7, [196,183,224]);
function crc32(b) { let c = 0xffffffff; for(const n of b) { c ^= n; for(let i=0;i<8;i++) c=(c>>>1)^((c&1)?0xedb88320:0); } return (c^0xffffffff)>>>0; }
function chunk(name,data) { const type=Buffer.from(name); const out=Buffer.alloc(data.length+12);out.writeUInt32BE(data.length);type.copy(out,4);data.copy(out,8);out.writeUInt32BE(crc32(Buffer.concat([type,data])),data.length+8);return out; }
const header=Buffer.alloc(13);header.writeUInt32BE(size);header.writeUInt32BE(size,4);header[8]=8;header[9]=2;
const output=path.join(__dirname,'../Fixtures/generation-demo.png');fs.mkdirSync(path.dirname(output),{recursive:true});
fs.writeFileSync(output,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',header),chunk('IDAT',deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]));
