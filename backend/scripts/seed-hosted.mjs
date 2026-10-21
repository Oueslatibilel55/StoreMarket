#!/usr/bin/env node
// Seeds a SMALL test data set into the HOSTED app (Render + Neon) through its public API — nothing touches the
// database directly. One account per role, two stores with a few products, a partnership and two orders.
//
//   API_URL=https://storemarket.onrender.com ADMIN_EMAIL=<your admin email> ADMIN_PASSWORD=<your password> \
//     node backend/scripts/seed-hosted.mjs
//
// The admin is YOUR account (made admin by BootstrapAdmin__Emails on Render); its password is only read from the
// environment, never written anywhere. The demo accounts get random passwords, saved next to this script in
// hosted-users.local.txt (git-ignored) and reused when the script runs again. Safe to re-run: it adds what is missing.

import { randomBytes } from 'node:crypto';
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { extname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { productTree, storeCategories, stores as catalogStores } from './demo-data/catalog.mjs';

const API = (process.env.API_URL ?? 'https://storemarket.onrender.com').replace(/\/$/, '');
const ADMIN_EMAIL = process.env.ADMIN_EMAIL;
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD;
const HERE = fileURLToPath(new URL('.', import.meta.url));
const IMAGES = join(HERE, 'demo-data', 'images');
const CREDENTIALS = join(HERE, 'hosted-users.local.txt');
const PRODUCTS_PER_STORE = 4;
const PHOTOS_PER_PRODUCT = 2;

if (!ADMIN_EMAIL || !ADMIN_PASSWORD) {
  console.error('Set ADMIN_EMAIL and ADMIN_PASSWORD (your admin account on the hosted site).');
  process.exit(1);
}

// One account per role. Store owners sign up as clients, then an admin approves their StoreOwner request (027).
const people = {
  client: { email: 'leila.gharbi@storemarket.demo', fullName: 'Leila Gharbi', phone: '50 112 233', accountType: 'Buyer' },
  affiliate: { email: 'sana.trabelsi@storemarket.demo', fullName: 'Sana Trabelsi', phone: '55 321 987', accountType: 'Affiliate' },
  owner: { email: 'karim.bensalah@storemarket.demo', fullName: 'Karim Ben Salah', phone: '22 145 678', accountType: 'Buyer', store: 'parfumerie-karim' },
  owner2: { email: 'amel.zouari@storemarket.demo', fullName: 'Amel Zouari', phone: '22 693 580', accountType: 'Buyer', store: 'douceurs-d-amel' },
};

// -- Passwords: reuse the saved ones, generate the missing ---------------------------------------------------------
const saved = existsSync(CREDENTIALS)
  ? Object.fromEntries(readFileSync(CREDENTIALS, 'utf8').split('\n').map((l) => l.trim().split(/\s+/)).filter((p) => p.length === 2 && p[0].includes('@')))
  : {};
for (const p of Object.values(people)) p.password = saved[p.email] ?? `Sm-${randomBytes(9).toString('base64url')}7a`;
writeFileSync(CREDENTIALS, `# Demo accounts on ${API} (seed-hosted.mjs). Git-ignored — do not commit.\n`
  + Object.entries(people).map(([role, p]) => `${p.email} ${p.password}`.padEnd(70) + `\n`).join('').trimEnd()
  + `\n# roles: client, affiliate, store owner (parfumerie-karim), store owner (douceurs-d-amel)\n`);

// -- HTTP ---------------------------------------------------------------------------------------------------------
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
async function call(method, path, { token, body, form } = {}) {
  for (let attempt = 0; ; attempt++) {
    const res = await fetch(`${API}${path}`, {
      method,
      headers: { ...(form ? {} : { 'Content-Type': 'application/json' }), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: form ?? (body ? JSON.stringify(body) : undefined),
    });
    // Auth endpoints allow 10 requests / minute / IP: wait and retry.
    if (res.status === 429 && attempt < 12) {
      if (attempt === 0) console.log('  rate limited by the API, waiting…');
      await sleep(10_000);
      continue;
    }
    const text = await res.text();
    let data = null;
    try { data = text ? JSON.parse(text) : null; } catch { data = text; }
    return { status: res.status, data };
  }
}
const must = (res, what, ...ok) => {
  if (!(ok.length ? ok : [200, 201, 204]).includes(res.status)) throw new Error(`${what}: ${res.status} ${res.data?.code ?? JSON.stringify(res.data)?.slice(0, 200)}`);
  return res.data;
};
const login = async (email, password) => must(await call('POST', '/api/auth/login', { body: { email, password } }), `sign in ${email}`);

async function signUpOrIn(p) {
  const reg = await call('POST', '/api/auth/register', {
    body: { email: p.email, password: p.password, fullName: p.fullName, phone: p.phone, accountType: p.accountType, acceptAffiliateTerms: p.accountType === 'Affiliate' },
  });
  if (reg.status === 200) return { ...reg.data, created: true };
  return { ...(await login(p.email, p.password)), created: false };
}

// -- Images from ./demo-data/images ---------------------------------------------------------------------------------
const MIME = { '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.png': 'image/png', '.webp': 'image/webp' };
const isImage = (f) => MIME[extname(f).toLowerCase()] !== undefined;
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
async function upload(token, purpose, file, name) {
  if (!file) return null;
  const form = new FormData();
  form.append('file', new Blob([readFileSync(file)], { type: MIME[extname(file).toLowerCase()] }), name + extname(file));
  form.append('purpose', purpose);
  return must(await call('POST', '/api/images', { token, form }), `upload ${name}`, 201).id;
}

// -- 0. Wake the API up (free Render instances sleep) ---------------------------------------------------------------
process.stdout.write(`API ${API} `);
for (let i = 0; ; i++) {
  const ok = await fetch(`${API}/health`).then((r) => r.ok).catch(() => false);
  if (ok) break;
  if (i > 20) throw new Error('the API does not answer /health');
  process.stdout.write('.');
  await sleep(5000);
}
console.log('up');

// -- 1. Admin (your account) ----------------------------------------------------------------------------------------
const admin = await login(ADMIN_EMAIL, ADMIN_PASSWORD);
if (!admin.user.roles.includes('Admin')) throw new Error(`${ADMIN_EMAIL} is not Admin — check BootstrapAdmin__Emails__0 on Render, then sign in once on the site.`);
const adminToken = admin.accessToken;
console.log(`admin    ${ADMIN_EMAIL}`);

// -- 2. Demo accounts -------------------------------------------------------------------------------------------------
const sessions = {};
for (const [role, p] of Object.entries(people)) {
  sessions[role] = await signUpOrIn(p);
  console.log(`${sessions[role].created ? 'created ' : 'exists  '} ${role.padEnd(9)} ${p.email}`);
}

// -- 3. Store owners: StoreOwner request approved by the admin, then a fresh token with the role ----------------------
for (const role of ['owner', 'owner2']) {
  const p = people[role];
  let s = sessions[role];
  if (!s.user.roles.includes('StoreOwner')) {
    const mine = must(await call('GET', '/api/me/role-requests', { token: s.accessToken }), 'my role requests');
    let request = mine.find((r) => r.role === 'StoreOwner' && r.status === 'Pending');
    request ??= must(await call('POST', '/api/me/role-requests', { token: s.accessToken, body: { role: 'StoreOwner', message: 'Ouverture de ma boutique (démo)' } }), 'role request', 200, 201);
    must(await call('POST', `/api/admin/role-requests/${request.id}/approve`, { token: adminToken }), 'approve StoreOwner');
    s = sessions[role] = await login(p.email, p.password);
    console.log(`approved ${p.email} → StoreOwner`);
  }
}

// -- 4. Store categories + product category tree (admin) ---------------------------------------------------------------
const existingStoreCategories = new Set(must(await call('GET', '/api/admin/store-categories', { token: adminToken }), 'store categories').map((c) => c.slug));
for (const c of storeCategories.filter((x) => !existingStoreCategories.has(x.slug))) {
  const { color, ...fields } = c;
  const imageId = await upload(adminToken, 'CategoryImage', findImage('store-categories', c.slug), c.slug);
  must(await call('POST', '/api/admin/store-categories', { token: adminToken, body: { ...fields, imageId } }), `store category ${c.slug}`, 201);
  console.log(`category ${c.slug}`);
}
let productCategories = must(await call('GET', '/api/admin/product-categories', { token: adminToken }), 'product categories');
async function seedProductCategory(node, parentId) {
  let id = productCategories.find((c) => c.slug === node.slug)?.id;
  if (!id) {
    id = must(await call('POST', '/api/admin/product-categories', {
      token: adminToken, body: { parentId, nameFr: node.fr, nameAr: node.ar, nameEn: node.en, slug: node.slug },
    }), `product category ${node.slug}`, 201).id;
    for (const a of node.attrs ?? []) must(await call('POST', `/api/admin/product-categories/${id}/attributes`, { token: adminToken, body: a }), `field of ${node.slug}`, 201);
    console.log(`products ${node.slug}`);
  }
  for (const child of node.children ?? []) await seedProductCategory(child, id);
}
for (const node of productTree) await seedProductCategory(node, null);
productCategories = must(await call('GET', '/api/admin/product-categories', { token: adminToken }), 'product categories');
const storeCategoryIds = new Map(must(await call('GET', '/api/admin/store-categories', { token: adminToken }), 'store categories').map((c) => [c.slug, c.id]));
const productCategoryIds = new Map(productCategories.map((c) => [c.slug, c.id]));

// -- 5. Two stores with a few products (owners) --------------------------------------------------------------------------
const schemas = new Map();
async function schemaOf(slug) {
  if (!schemas.has(slug)) {
    const res = must(await call('GET', `/api/product-categories/${slug}/schema`), `schema ${slug}`);
    schemas.set(slug, new Map(res.attributes.map((a) => [a.attribute.key, a.attribute.type])));
  }
  return schemas.get(slug);
}
function toValue(types, key, v) {
  const type = types.get(key);
  if (type === 'SingleChoice' || type === 'MultiChoice') return { key, options: Array.isArray(v) ? v : [v] };
  if (type === 'Number') return { key, number: v };
  if (type === 'Boolean') return { key, boolean: v };
  return { key, text: String(v) };
}
const allCities = must(await call('GET', '/api/cities'), 'cities');

for (const role of ['owner', 'owner2']) {
  const p = people[role];
  const token = sessions[role].accessToken;
  const s = catalogStores.find((x) => x.slug === p.store);
  if ((await call('GET', '/api/me/store', { token })).status !== 200) {
    const logo = await upload(token, 'StoreLogo', findImage(join('stores', s.slug), 'logo'), `${s.slug}-logo`);
    const banner = await upload(token, 'StoreBanner', findImage(join('stores', s.slug), 'banner'), `${s.slug}-banner`);
    must(await call('POST', '/api/me/store', {
      token,
      body: {
        name: s.name, slug: s.slug, categoryId: storeCategoryIds.get(s.category), description: s.description,
        phone: p.phone, city: s.city, address: null, logoImageId: logo, bannerImageId: banner,
        defaultAffiliatePercent: s.percent, deliveryFee: s.fee, deliveryCities: s.cities === 'all' ? allCities : s.cities,
        deliveryMinDays: s.days[0], deliveryMaxDays: s.days[1], facebookUrl: null, instagramUrl: null, websiteUrl: null,
      },
    }), `store ${s.slug}`, 201);
    console.log(`store    ${s.slug}`);
  }
  const existing = new Set((must(await call('GET', '/api/me/store/products?pageSize=100', { token }), 'my products').items ?? []).map((x) => x.slug));
  for (const prod of s.products.filter((x) => !x.variants).slice(0, PRODUCTS_PER_STORE).filter((x) => !existing.has(x.slug))) {
    const files = findImages('products', prod.slug).slice(0, PHOTOS_PER_PRODUCT);
    const imageIds = (await Promise.all(files.map((f, i) => upload(token, 'ProductPhoto', f, `${prod.slug}-${i + 1}`)))).filter(Boolean);
    const types = await schemaOf(prod.category);
    must(await call('POST', '/api/me/store/products', {
      token,
      body: {
        name: prod.name, slug: prod.slug, categoryId: productCategoryIds.get(prod.category), description: prod.description ?? null,
        price: prod.price, compareAtPrice: prod.compare ?? null, sku: null, stock: Math.max(prod.stock ?? 0, 20), affiliatePercent: prod.percent ?? null,
        status: 'Published', imageIds, values: Object.entries(prod.values ?? {}).map(([k, v]) => toValue(types, k, v)), variantAxes: [], variants: [],
      },
    }), `product ${prod.slug}`, 201);
    console.log(`product  ${prod.slug} (${imageIds.length} photo${imageIds.length === 1 ? '' : 's'})`);
  }
}

// -- 6. The affiliate works with both stores --------------------------------------------------------------------------------
const affiliate = sessions.affiliate;
const code = affiliate.user.affiliateCode;
const myPartnerships = must(await call('GET', '/api/me/partnerships', { token: affiliate.accessToken }), 'partnerships');
for (const role of ['owner', 'owner2']) {
  const store = must(await call('GET', `/api/stores/${people[role].store}`), 'store');
  if (myPartnerships.some((m) => m.store.id === store.id)) continue;
  const asked = must(await call('POST', '/api/me/partnerships', { token: affiliate.accessToken, body: { storeId: store.id, message: 'Je partage vos produits sur Instagram' } }), 'partnership', 201);
  must(await call('POST', `/api/me/store/partnerships/${asked.id}/accept`, { token: sessions[role].accessToken }), 'accept partnership');
  console.log(`partner  ${people.affiliate.email} → ${store.slug} (accepted)`);
}

// -- 7. Orders: the client buys with the affiliate code; one store delivers (coins), the other is still to confirm --------
const client = sessions.client;
const myOrders = must(await call('GET', '/api/me/orders', { token: client.accessToken }), 'my orders');
if ((myOrders.items ?? myOrders).length === 0) {
  for (const role of ['owner', 'owner2']) {
    const products = must(await call('GET', `/api/products?store=${people[role].store}&pageSize=2`), 'store products').items ?? [];
    for (const [i, prod] of products.slice(0, 2).entries())
      must(await call('POST', '/api/me/cart/lines', { token: client.accessToken, body: { productId: prod.id, quantity: i + 1, affiliateCode: code } }), 'cart line');
  }
  const order = must(await call('POST', '/api/orders', {
    token: client.accessToken,
    body: { contact: { fullName: people.client.fullName, phone: people.client.phone, city: 'tunis', address: '12 rue de Marseille, Tunis' } },
  }), 'order', 200, 201);
  console.log(`order    ${order.reference} (${order.storeOrders.length} stores)`);

  const karim = must(await call('GET', `/api/stores/${people.owner.store}`), 'store');
  const delivered = order.storeOrders.find((s) => s.storeId === karim.id);
  for (const status of ['Confirmed', 'Shipped', 'Delivered'])
    must(await call('POST', `/api/me/store/orders/${delivered.id}/status`, { token: sessions.owner.accessToken, body: { status } }), `status ${status}`);
  console.log(`delivered ${order.reference} · ${karim.name} → coins credited to ${code}`);
}

const coins = must(await call('GET', '/api/me/coins', { token: (await login(people.affiliate.email, people.affiliate.password)).accessToken }), 'coins');
console.log(`\ncoins of ${people.affiliate.email}: balance ${coins.balance}, expected ${coins.expected}`);
console.log(`Accounts and passwords: ${CREDENTIALS}`);
