// Writes .br and .gz copies of the production build's text assets at maximum compression, once, at build time.
// The API serves them when the browser accepts them (SpaHosting), so no request pays for compressing a bundle
// (M12: Brotli quality 11 is ~300 ms per bundle, far too slow per request, but ~25 % smaller than quality 1).
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { brotliCompressSync, constants, gzipSync } from 'node:zlib';

const root = new URL('../dist/web/browser/', import.meta.url);
const compressible = /\.(js|css|html|svg|txt|json)$/;
let files = 0;
let before = 0;
let after = 0;

function walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) {
      walk(path);
    } else if (compressible.test(name)) {
      const data = readFileSync(path);
      const br = brotliCompressSync(data, {
        params: {
          [constants.BROTLI_PARAM_QUALITY]: 11,
          [constants.BROTLI_PARAM_SIZE_HINT]: data.length,
        },
      });
      writeFileSync(`${path}.br`, br);
      writeFileSync(`${path}.gz`, gzipSync(data, { level: 9 }));
      files++;
      before += data.length;
      after += br.length;
    }
  }
}

walk(root.pathname.replace(/^\/([A-Za-z]:)/, '$1'));
console.log(
  `precompressed ${files} files: ${Math.round(before / 1024)} kB -> ${Math.round(after / 1024)} kB (brotli)`,
);
