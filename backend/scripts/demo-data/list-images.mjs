#!/usr/bin/env node
// Creates the image folders of the demo catalogue and writes images/README.md (which file goes where),
// then checks the catalogue: every product value / variant must match its category fields.
//
//   node backend/scripts/demo-data/list-images.mjs        (re-run after editing catalog.mjs)

import { existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { productTree, storeCategories, stores } from './catalog.mjs';

const root = join(fileURLToPath(new URL('.', import.meta.url)), 'images');
const count = (dir) => (existsSync(dir) ? readdirSync(dir).filter((f) => /\.(jpe?g|png|webp)$/i.test(f)).length : 0);
const has = (dir, name) => existsSync(dir) && readdirSync(dir).some((f) => f.toLowerCase().startsWith(name + '.'));

const lines = [
  '# Demo images', '',
  'Drop your photos here, then run from `backend/`:', '',
  '```', 'node scripts/seed-dev-users.mjs --reset-catalog', '```', '',
  '- Formats: **JPEG, PNG or WebP**, **5 MB max** each (setting `upload.maxImageSizeMb`). No SVG / GIF.',
  '- Any file name inside a product folder works; they are taken in name order (`1.jpg`, `2.jpg`…), **the first one is the main photo**, 8 max.',
  '- Square photos (e.g. 1000 × 1000) look best for products; banners are wide (e.g. 1600 × 500); logos square.',
  '- Missing image = a plain colour tile: add photos little by little and re-run.',
  "- Use photos you own or that are free to use (your own pictures, the store's, free-licence sites like Unsplash / Pexels). Brand / shop photos are fine for local tests only.",
  '- Most photos come from Wikimedia Commons (free licences) — author, licence and source of each are in `CREDITS.md`. Replace any of them by dropping your own file in the folder.',
  '- ✅ = an image is already there.', '',
  '## Store categories — `store-categories/<name>.jpg`', '',
];
mkdirSync(join(root, 'store-categories'), { recursive: true });
for (const c of storeCategories) lines.push(`- ${has(join(root, 'store-categories'), c.slug) ? '✅' : '⬜'} \`store-categories/${c.slug}.jpg\` — ${c.nameFr}`);

lines.push('', '## Stores — `stores/<store>/logo.jpg` and `banner.jpg`', '');
for (const s of stores) {
  const dir = join(root, 'stores', s.slug);
  mkdirSync(dir, { recursive: true });
  lines.push(`- ${has(dir, 'logo') ? '✅' : '⬜'} logo, ${has(dir, 'banner') ? '✅' : '⬜'} banner — \`stores/${s.slug}/\` — ${s.name}`);
}

lines.push('', '## Products — `products/<product>/1.jpg`, `2.jpg`…', '');
for (const s of stores) {
  lines.push(`### ${s.name}`, '');
  for (const p of s.products) {
    const dir = join(root, 'products', p.slug);
    mkdirSync(dir, { recursive: true });
    const n = count(dir);
    lines.push(`- ${n ? `✅ ${n}` : '⬜'} \`products/${p.slug}/\` — ${p.name}`);
  }
  lines.push('');
}
writeFileSync(join(root, 'README.md'), lines.join('\n'));

// -- Check the catalogue against the category fields (keys made like the API does) ------------------
const slug = (t) => t.normalize('NFD').replace(/\p{Mn}/gu, '').toLowerCase().replace(/[^a-z0-9]/g, '-').replace(/-{2,}/g, '-').replace(/^-|-$/g, '');
const nodes = new Map();
const walk = (n, parent) => { nodes.set(n.slug, { ...n, parent }); (n.children ?? []).forEach((c) => walk(c, n.slug)); };
productTree.forEach((n) => walk(n, null));
const fieldsOf = (category) => {
  const fields = [];
  for (let n = nodes.get(category); n; n = nodes.get(n.parent)) {
    fields.unshift(...n.attrs.map((a) => ({ key: slug(a.nameFr), type: a.type, required: a.required, options: a.options.map((o) => slug(o.name.fr)) })));
  }
  return fields;
};
const errors = [];
const storeCategorySlugs = new Set(storeCategories.map((c) => c.slug));
const productSlugs = new Set();
for (const s of stores) {
  if (!storeCategorySlugs.has(s.category)) errors.push(`${s.slug}: unknown store category ${s.category}`);
  for (const p of s.products) {
    if (productSlugs.has(p.slug)) errors.push(`${p.slug}: slug used twice`);
    productSlugs.add(p.slug);
    if (!nodes.has(p.category)) { errors.push(`${p.slug}: unknown category ${p.category}`); continue; }
    const fields = fieldsOf(p.category);
    const byKey = new Map(fields.map((f) => [f.key, f]));
    if (byKey.size !== fields.length) errors.push(`${p.category}: a field key is used twice in the branch`);
    const axes = p.variants?.axes ?? [];
    for (const [k, v] of Object.entries(p.values ?? {})) {
      const f = byKey.get(k);
      if (!f) errors.push(`${p.slug}: field ${k} is not in ${p.category}`);
      else if (f.type.endsWith('Choice')) for (const o of [].concat(v)) if (!f.options.includes(o)) errors.push(`${p.slug}: ${k} has no choice ${o}`);
      if (axes.includes(k)) errors.push(`${p.slug}: ${k} is a variant axis, give it per variant`);
    }
    for (const a of axes) {
      const f = byKey.get(a);
      if (!f?.type.endsWith('Choice')) { errors.push(`${p.slug}: variant axis ${a} is not a choice field`); continue; }
      for (const row of p.variants.rows) if (!f.options.includes(row[axes.indexOf(a)])) errors.push(`${p.slug}: variant ${row} — ${a} has no choice ${row[axes.indexOf(a)]}`);
    }
    for (const f of fields.filter((x) => x.required && !axes.includes(x.key))) {
      if (!(f.key in (p.values ?? {})) && (p.status ?? 'Published') === 'Published') errors.push(`${p.slug}: required field ${f.key} missing`);
    }
  }
}
console.log(`${stores.length} stores, ${productSlugs.size} products — images/README.md written`);
if (errors.length) {
  console.error(errors.join('\n'));
  process.exit(1);
}
console.log('catalogue OK');
