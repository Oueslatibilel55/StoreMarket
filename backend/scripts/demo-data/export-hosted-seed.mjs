#!/usr/bin/env node
// Builds the small demo data set the API seeds on startup when DemoSeed__Password is set (hosted app, ADR 0006):
// src/WebApp.Api/DemoSeed/demo-seed.json (request bodies, same shapes as the API) + the few images it uses.
// Source: ./catalog.mjs and ./images. Re-run after changing the catalogue:  node backend/scripts/demo-data/export-hosted-seed.mjs

import { copyFileSync, existsSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, extname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { productTree, storeCategories, stores } from './catalog.mjs';

const HERE = fileURLToPath(new URL('.', import.meta.url));
const IMAGES = join(HERE, 'images');
const OUT = join(HERE, '..', '..', 'src', 'WebApp.Api', 'DemoSeed');
const STORES = { 'parfumerie-karim': 'owner', 'douceurs-d-amel': 'owner2' };
const PRODUCTS_PER_STORE = 4;
const PHOTOS_PER_PRODUCT = 2;

const isImage = (f) => ['.jpg', '.jpeg', '.png', '.webp'].includes(extname(f).toLowerCase());
function findImage(folder, name) {
  const dir = join(IMAGES, folder);
  if (!existsSync(dir)) return null;
  const file = readdirSync(dir).find((f) => isImage(f) && f.slice(0, -extname(f).length).toLowerCase() === name.toLowerCase());
  return file ? join(dir, file) : null;
}
function findImages(folder, sub) {
  const dir = join(IMAGES, folder, sub);
  return existsSync(dir) ? readdirSync(dir).filter(isImage).sort((a, b) => a.localeCompare(b, undefined, { numeric: true })).map((f) => join(dir, f)) : [];
}

rmSync(join(OUT, 'images'), { recursive: true, force: true });
/** Copies the image next to the seed and returns its path relative to DemoSeed/images (or null). */
function keep(file) {
  if (!file) return null;
  const rel = relative(IMAGES, file).replaceAll('\\', '/');
  const target = join(OUT, 'images', rel);
  mkdirSync(dirname(target), { recursive: true });
  copyFileSync(file, target);
  return rel;
}

const productCategories = [];
(function walk(nodes, parentSlug) {
  for (const n of nodes) {
    productCategories.push({ parentSlug, body: { nameFr: n.fr, nameAr: n.ar, nameEn: n.en, slug: n.slug }, attributes: n.attrs ?? [] });
    walk(n.children ?? [], n.slug);
  }
})(productTree, null);

const seed = {
  storeCategories: storeCategories.map(({ color, ...body }) => ({ body, image: keep(findImage('store-categories', body.slug)) })),
  productCategories,
  stores: stores.filter((s) => STORES[s.slug]).map((s) => ({
    owner: STORES[s.slug],
    categorySlug: s.category,
    body: {
      name: s.name, slug: s.slug, description: s.description, city: s.city, defaultAffiliatePercent: s.percent,
      deliveryFee: s.fee, deliveryCities: s.cities === 'all' ? null : s.cities, deliveryMinDays: s.days[0], deliveryMaxDays: s.days[1],
    },
    logo: keep(findImage(join('stores', s.slug), 'logo')),
    banner: keep(findImage(join('stores', s.slug), 'banner')),
    products: s.products.filter((p) => !p.variants).slice(0, PRODUCTS_PER_STORE).map((p) => ({
      categorySlug: p.category,
      body: { name: p.name, slug: p.slug, description: p.description ?? null, price: p.price, compareAtPrice: p.compare ?? null,
        stock: Math.max(p.stock ?? 0, 20), affiliatePercent: p.percent ?? null },
      values: p.values ?? {},
      images: findImages('products', p.slug).slice(0, PHOTOS_PER_PRODUCT).map(keep),
    })),
  })),
};

mkdirSync(OUT, { recursive: true });
writeFileSync(join(OUT, 'demo-seed.json'), JSON.stringify(seed, null, 2) + '\n');
console.log(`${seed.storeCategories.length} store categories, ${productCategories.length} product categories, `
  + `${seed.stores.length} stores, ${seed.stores.reduce((n, s) => n + s.products.length, 0)} products → ${relative(process.cwd(), OUT)}`);
