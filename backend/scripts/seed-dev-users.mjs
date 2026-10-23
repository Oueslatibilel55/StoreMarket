#!/usr/bin/env node
// Seeds demo data into the LOCAL dev database through the real API (hashing, affiliate codes,
// role requests, uploads, audit log all happen for real): demo accounts, then the catalogue of
// ./demo-data/catalog.mjs with the images of ./demo-data/images (see its README).
//
//   node backend/scripts/seed-dev-users.mjs                    add what is missing (safe to re-run)
//   node backend/scripts/seed-dev-users.mjs --reset-catalog    wipe stores, products, categories and
//                                                              images first, then rebuild them (users stay)
//
// API must run on http://localhost:5185. Done outside the API: promoting the first admin (same effect
// as BootstrapAdmin:Emails) and --reset-catalog, directly in the Aspire Postgres container.
// DEV ONLY — these are throw-away demo accounts, never use them in production.

import { execFileSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { extname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { crc32, deflateSync } from 'node:zlib';
import { productTree, storeCategories, storeOwners, stores } from './demo-data/catalog.mjs';

const API = process.env.API_URL ?? 'http://localhost:5185';
const PASSWORD = 'Demo2026x'; // shared password of every demo account
const CONTAINER = 'webapp-postgres-aspire';
const HERE = fileURLToPath(new URL('.', import.meta.url));
const IMAGES = join(HERE, 'demo-data', 'images');
const UPLOADS = join(HERE, '..', 'src', 'WebApp.Api', 'uploads');
const RESET_CATALOG = process.argv.includes('--reset-catalog');

// accountType: what the user picks at sign-up — 'Buyer' (client account) or 'Affiliate'.
// requests: roles asked for afterwards, and what happens to them:
//   decide: 'approve' | 'refuse' (admin, audited) | 'cancel' (the user withdraws) | none (stays pending).
// blocked: reason, to have the admin block the account.
const users = [
  // -- One account per role (see dev-users.xlsx) -------------------------------------
  { email: 'admin@webapp.local', fullName: 'Admin Démo', phone: '20 000 001', accountType: 'Buyer', admin: true },
  { email: 'leila.gharbi@webapp.local', fullName: 'Leila Gharbi', phone: '50 112 233', accountType: 'Buyer' },
  { email: 'sana.trabelsi@webapp.local', fullName: 'Sana Trabelsi', phone: '55 321 987', accountType: 'Affiliate' },
  { email: 'karim.bensalah@webapp.local', fullName: 'Karim Ben Salah', phone: '22 145 678', accountType: 'Buyer',
    requests: [{ role: 'StoreOwner', message: 'Parfumerie à Sousse', decide: 'approve' }] },

  // -- More test data ----------------------------------------------------------------
  // Buyers
  { email: 'ines.chaabane@webapp.local', fullName: 'Ines Chaabane', phone: '21 556 677', accountType: 'Buyer' },
  { email: 'yasmine.ferchichi@webapp.local', fullName: 'Yasmine Ferchichi', phone: '26 410 852', accountType: 'Buyer',
    requests: [{ role: 'Affiliate', message: 'Je veux essayer', decide: 'cancel' }] },
  { email: 'mehdi.bouazizi@webapp.local', fullName: 'Mehdi Bouazizi', phone: '92 334 455', accountType: 'Buyer',
    requests: [{ role: 'Affiliate', message: "J'ai une page Facebook de 20 000 abonnés" }] },
  { email: 'omar.khelifi@webapp.local', fullName: 'Omar Khelifi', phone: '97 220 314', accountType: 'Buyer',
    requests: [{ role: 'StoreOwner', message: 'Magasin de chaussures à Sfax' }] },
  { email: 'rania.saidi@webapp.local', fullName: 'Rania Saidi', phone: '58 963 147', accountType: 'Buyer',
    requests: [{ role: 'StoreOwner', decide: 'refuse', reason: 'Aucune information sur la boutique' }] },
  { email: 'fatma.dridi@webapp.local', fullName: 'Fatma Dridi', phone: '23 741 096', accountType: 'Buyer',
    requests: [{ role: 'Affiliate', message: 'Influenceuse beauté', decide: 'approve' }] },
  // Affiliates
  { email: 'youssef.mansour@webapp.local', fullName: 'Youssef Mansour', phone: '24 789 456', accountType: 'Affiliate' },
  { email: 'walid.ayari@webapp.local', fullName: 'Walid Ayari', phone: '29 158 736', accountType: 'Affiliate' },
  { email: 'nesrine.toumi@webapp.local', fullName: 'Nesrine Toumi', phone: '54 302 619', accountType: 'Affiliate',
    requests: [{ role: 'Buyer', message: 'Je veux aussi acheter pour moi', decide: 'approve' }] },
  { email: 'hela.jebali@webapp.local', fullName: 'Hela Jebali', phone: '50 874 225', accountType: 'Affiliate',
    requests: [{ role: 'StoreOwner', message: 'Bijoux faits main' }] },
  { email: 'amine.jaziri@webapp.local', fullName: 'Amine Jaziri', phone: '29 654 321', accountType: 'Affiliate',
    requests: [{ role: 'StoreOwner', message: 'Je veux ouvrir ma boutique de cosmétiques', decide: 'approve' }] },
  // Store owners (client account + approved request)
  { email: 'nour.haddad@webapp.local', fullName: 'Nour Haddad', phone: '98 456 123', accountType: 'Buyer',
    requests: [{ role: 'StoreOwner', message: 'Vêtements femme, Tunis', decide: 'approve' }] },
  { email: 'slim.benamor@webapp.local', fullName: 'Slim Ben Amor', phone: '98 117 402', accountType: 'Buyer',
    requests: [{ role: 'StoreOwner', message: 'Électronique et accessoires, Ariana', decide: 'approve' }] },
  { email: 'amel.zouari@webapp.local', fullName: 'Amel Zouari', phone: '22 693 580', accountType: 'Buyer',
    requests: [
      { role: 'StoreOwner', message: 'Pâtisserie et douceurs, Nabeul', decide: 'approve' },
      { role: 'Affiliate', message: 'Je recommande aussi d\'autres boutiques' },
    ] },
  // Owners of the other catalogue stores (demo-data/catalog.mjs)
  ...storeOwners.map((o) => ({ ...o, accountType: 'Buyer', requests: [{ role: 'StoreOwner', message: 'Ouverture de ma boutique', decide: 'approve' }] })),
  // Blocked
  { email: 'fake.seller@webapp.local', fullName: 'Faux Vendeur', phone: '23 999 000', accountType: 'Buyer',
    blocked: 'Produits contrefaits signalés' },
  { email: 'spam.affiliate@webapp.local', fullName: 'Spam Affilié', phone: '23 999 001', accountType: 'Affiliate',
    blocked: 'Faux avis et spam de code affilié' },
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function call(method, path, { token, body, form } = {}) {
  // Auth endpoints allow 10 requests / minute / IP (ADR 0004): wait and retry on 429.
  for (let attempt = 0; ; attempt++) {
    const res = await fetch(`${API}${path}`, {
      method,
      headers: { ...(form ? {} : { 'Content-Type': 'application/json' }), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: form ?? (body ? JSON.stringify(body) : undefined),
    });
    if (res.status === 429 && attempt < 12) {
      if (attempt === 0) console.log('  rate limited by the API, waiting…');
      await sleep(10_000);
      continue;
    }
    const text = await res.text();
    return { status: res.status, data: text ? JSON.parse(text) : null };
  }
}

async function signUpOrIn(u) {
  const reg = await call('POST', '/api/auth/register', {
    body: {
      email: u.email, password: PASSWORD, fullName: u.fullName, phone: u.phone,
      accountType: u.accountType, acceptAffiliateTerms: u.accountType === 'Affiliate',
    },
  });
  if (reg.status === 200) return { ...reg.data, created: true };
  const login = await call('POST', '/api/auth/login', { body: { email: u.email, password: PASSWORD } });
  if (login.status === 200) return { ...login.data, created: false };
  // Re-run: a demo account blocked last time can't sign in any more — that's the expected state.
  if (u.blocked && login.data?.code === 'auth.accountBlocked') return { created: false, alreadyBlocked: true };
  throw new Error(`${u.email}: register ${reg.status} ${reg.data?.code ?? ''}, login ${login.status} ${login.data?.code ?? ''}`);
}

/** A plain-colour 64×64 PNG, built by hand (no image library needed) — used when no photo is given. */
function colourTile([r, g, b]) {
  const size = 64;
  const chunk = (type, data) => {
    const len = Buffer.alloc(4);
    len.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([len, body, crc]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header[8] = 8; // bit depth
  header[9] = 2; // RGB
  const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(size * 3).map((_, i) => [r, g, b][i % 3])]);
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(Buffer.concat(Array.from({ length: size }, () => row)))),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

// -- Images from ./demo-data/images ------------------------------------------------------------------
const MIME = { '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.png': 'image/png', '.webp': 'image/webp' };
const isImage = (name) => MIME[extname(name).toLowerCase()] !== undefined;

/** images/<folder>/<name>.jpg|jpeg|png|webp, or null. */
function findImage(folder, name) {
  const dir = join(IMAGES, folder);
  if (!existsSync(dir)) return null;
  const file = readdirSync(dir).find((f) => isImage(f) && f.slice(0, -extname(f).length).toLowerCase() === name.toLowerCase());
  return file ? join(dir, file) : null;
}

/** Every image of images/<folder>/<sub>/, sorted by name (1.jpg, 2.jpg…). */
function findImages(folder, sub) {
  const dir = join(IMAGES, folder, sub);
  return existsSync(dir) ? readdirSync(dir).filter(isImage).sort((a, b) => a.localeCompare(b, undefined, { numeric: true })).map((f) => join(dir, f)) : [];
}

/** Uploads the file when given, else a colour tile. Returns the image id. */
async function upload(token, purpose, file, color, name) {
  const form = new FormData();
  if (file) form.append('file', new Blob([readFileSync(file)], { type: MIME[extname(file).toLowerCase()] }), name + extname(file));
  else form.append('file', new Blob([colourTile(color)], { type: 'image/png' }), `${name}.png`);
  form.append('purpose', purpose);
  const res = await call('POST', '/api/images', { token, form });
  if (res.status !== 201) throw new Error(`upload ${name}: ${res.status} ${res.data?.code ?? ''} (max size is the setting upload.maxImageSizeMb)`);
  return res.data.id;
}

const sql = (query) =>
  execFileSync('docker', ['exec', '-e', 'PGPASSWORD=webapp_dev', CONTAINER, 'psql', '-U', 'webapp', '-d', 'webapp', '-Atc', query], { encoding: 'utf8' });

const health = await fetch(`${API}/health`).catch(() => null);
if (!health?.ok) {
  console.error(`API not reachable at ${API} — start it with: dotnet run --project src/WebApp.AppHost`);
  process.exit(1);
}

// 0. --reset-catalog: the catalogue goes, users / roles / settings / audit stay
if (RESET_CATALOG) {
  sql('truncate product_variants, products, product_attributes, product_categories, stores, store_categories, images;');
  if (existsSync(UPLOADS)) for (const entry of readdirSync(UPLOADS)) rmSync(join(UPLOADS, entry), { recursive: true, force: true });
  console.log('catalog  reset (stores, products, categories, images)');
}

// 1. Everyone signs up with their account type (or signs in, when re-running)
const sessions = {};
for (const u of users) {
  sessions[u.email] = await signUpOrIn(u);
  console.log(`${sessions[u.email].created ? 'created ' : 'exists  '} ${u.email}`);
}

// 2. First admin (what BootstrapAdmin:Emails does), then sign in again to get a token with the Admin role
const admin = users.find((u) => u.admin);
sql(`update users set roles = array_append(roles, 4) where email = '${admin.email}' and not (4 = any(roles));`);
const adminToken = (await call('POST', '/api/auth/login', { body: { email: admin.email, password: PASSWORD } })).data.accessToken;

// 3. Users ask for extra roles (an affiliate request accepts the terms)
const decisions = [];
for (const u of users.filter((x) => x.requests && !sessions[x.email].alreadyBlocked)) {
  const { accessToken, user } = sessions[u.email];
  const mine = (await call('GET', '/api/me/role-requests', { token: accessToken })).data;
  for (const r of u.requests) {
    if (user.roles.includes(r.role) || mine.some((m) => m.role === r.role)) continue;
    const res = await call('POST', '/api/me/role-requests', {
      token: accessToken, body: { role: r.role, message: r.message ?? null, acceptTerms: r.role === 'Affiliate' },
    });
    console.log(`request  ${u.email} → ${r.role}`);
    if (r.decide) decisions.push({ ...r, id: res.data.id, email: u.email, token: accessToken });
  }
}

// 4. Decisions: admin approves / refuses (audited), users withdraw
for (const d of decisions) {
  if (d.decide === 'approve') await call('POST', `/api/admin/role-requests/${d.id}/approve`, { token: adminToken });
  else if (d.decide === 'refuse') await call('POST', `/api/admin/role-requests/${d.id}/refuse`, { token: adminToken, body: { reason: d.reason } });
  else if (d.decide === 'cancel') await call('POST', `/api/me/role-requests/${d.id}/cancel`, { token: d.token });
  console.log(`${d.decide.padEnd(8)} ${d.email} → ${d.role}`);
}

// 5. Store categories (admin, audited) — only the missing ones; image images/store-categories/<slug>.*
const existingStoreCategories = new Set((await call('GET', '/api/admin/store-categories', { token: adminToken })).data.map((c) => c.slug));
for (const c of storeCategories.filter((x) => !existingStoreCategories.has(x.slug))) {
  const { color, ...fields } = c;
  const imageId = await upload(adminToken, 'CategoryImage', findImage('store-categories', c.slug), color, c.slug);
  const res = await call('POST', '/api/admin/store-categories', { token: adminToken, body: { ...fields, imageId } });
  if (res.status !== 201) throw new Error(`store category ${c.slug}: ${res.status} ${res.data?.code ?? ''}`);
  console.log(`category ${c.slug}`);
}

// 6. Product category tree with its fields (admin, audited) — only the missing categories
const existingProductCategories = new Set((await call('GET', '/api/admin/product-categories', { token: adminToken })).data.map((c) => c.slug));
async function seedProductCategory(node, parentId) {
  let id;
  if (existingProductCategories.has(node.slug)) {
    id = (await call('GET', '/api/admin/product-categories', { token: adminToken })).data.find((c) => c.slug === node.slug).id;
  } else {
    const created = await call('POST', '/api/admin/product-categories', {
      token: adminToken, body: { parentId, nameFr: node.fr, nameAr: node.ar, nameEn: node.en, slug: node.slug },
    });
    if (created.status !== 201) throw new Error(`product category ${node.slug}: ${created.status} ${created.data?.code ?? ''}`);
    id = created.data.id;
    for (const a of node.attrs ?? []) {
      const added = await call('POST', `/api/admin/product-categories/${id}/attributes`, { token: adminToken, body: a });
      if (added.status !== 201) throw new Error(`field ${a.nameFr} of ${node.slug}: ${added.status} ${added.data?.code ?? ''}`);
    }
    console.log(`products ${node.slug} (${(node.attrs ?? []).length} fields)`);
  }
  for (const child of node.children ?? []) await seedProductCategory(child, id);
}
for (const node of productTree) await seedProductCategory(node, null);

// 7. Stores and their products (owners, through the API) — only for owners without a store.
//    Images: images/stores/<store slug>/logo.*, banner.* and images/products/<product slug>/*.
const storeCategoryIds = new Map((await call('GET', '/api/admin/store-categories', { token: adminToken })).data.map((c) => [c.slug, c.id]));
const productCategoryIds = new Map((await call('GET', '/api/admin/product-categories', { token: adminToken })).data.map((c) => [c.slug, c.id]));
const schemas = new Map();
async function schemaOf(slug) {
  if (!schemas.has(slug)) {
    const res = await call('GET', `/api/product-categories/${slug}/schema`);
    schemas.set(slug, new Map(res.data.attributes.map((a) => [a.attribute.key, a.attribute.type])));
  }
  return schemas.get(slug);
}
/** A catalogue value → the request shape of its field type (checked against the category schema). */
function toValue(types, key, v, product) {
  const type = types.get(key);
  if (!type) throw new Error(`${product}: field "${key}" is not in its category`);
  if (type === 'SingleChoice' || type === 'MultiChoice') return { key, options: Array.isArray(v) ? v : [v] };
  if (type === 'Number') return { key, number: v };
  if (type === 'Boolean') return { key, boolean: v };
  return { key, text: String(v) };
}
const allCities = (await call('GET', '/api/cities')).data;

for (const s of stores) {
  const token = (await call('POST', '/api/auth/login', { body: { email: s.owner, password: PASSWORD } })).data.accessToken;
  if ((await call('GET', '/api/me/store', { token })).status === 200) continue;

  const owner = users.find((u) => u.email === s.owner);
  const logo = await upload(token, 'StoreLogo', findImage(join('stores', s.slug), 'logo'), s.color, `${s.slug}-logo`);
  const banner = await upload(token, 'StoreBanner', findImage(join('stores', s.slug), 'banner'), s.color.map((c) => Math.min(255, c + 60)), `${s.slug}-banner`);
  const created = await call('POST', '/api/me/store', {
    token,
    body: {
      name: s.name, slug: s.slug, categoryId: storeCategoryIds.get(s.category), description: s.description,
      phone: owner.phone, city: s.city, address: null, logoImageId: logo, bannerImageId: banner,
      defaultAffiliatePercent: s.percent, deliveryFee: s.fee, deliveryCities: s.cities === 'all' ? allCities : s.cities,
      deliveryMinDays: s.days[0], deliveryMaxDays: s.days[1],
      facebookUrl: `https://facebook.com/${s.slug}`, instagramUrl: `https://instagram.com/${s.slug.replace(/-/g, '.')}`, websiteUrl: null,
    },
  });
  if (created.status !== 201) throw new Error(`${s.owner}: store ${created.status} ${created.data?.code ?? ''}`);
  if (s.closed) await call('POST', '/api/me/store/close', { token });
  console.log(`store    ${s.slug}${s.closed ? ' (closed)' : ''}`);

  for (const p of s.products) {
    const files = findImages('products', p.slug).slice(0, 8);
    const imageIds = files.length
      ? await Promise.all(files.map((f, i) => upload(token, 'ProductPhoto', f, p.color, `${p.slug}-${i + 1}`)))
      : [await upload(token, 'ProductPhoto', null, p.color, p.slug)];
    const types = await schemaOf(p.category);
    const axes = p.variants?.axes ?? [];
    const body = {
      name: p.name, slug: p.slug, categoryId: productCategoryIds.get(p.category), description: p.description ?? null,
      price: p.price, compareAtPrice: p.compare ?? null, sku: null, stock: p.stock ?? 0, affiliatePercent: p.percent ?? null,
      status: p.status ?? 'Published', imageIds,
      values: Object.entries(p.values ?? {}).map(([k, v]) => toValue(types, k, v, p.slug)),
      variantAxes: axes,
      variants: (p.variants?.rows ?? []).map((row) => ({
        id: null, options: Object.fromEntries(axes.map((a, i) => [a, row[i]])), stock: row[axes.length], price: row[axes.length + 1] ?? null, sku: null,
      })),
    };
    const res = await call('POST', '/api/me/store/products', { token, body });
    if (res.status !== 201) throw new Error(`${s.slug}: product ${p.slug} ${res.status} ${res.data?.code ?? ''}`);
    console.log(`product  ${p.slug} (${files.length || 'no'} photo${files.length === 1 ? '' : 's'}${axes.length ? `, ${body.variants.length} variants` : ''})`);
  }
}

// 8. Partnerships (012): the affiliate asks, the store owner decides — only for affiliate/store pairs without one yet.
//    decide: 'accept' | 'refuse' | 'end' (accepted, then ended by the owner) | none (stays waiting).
const partnerships = [
  { affiliate: 'sana.trabelsi@webapp.local', store: 'parfumerie-karim', message: 'Je fais des vidéos parfums sur TikTok', decide: 'accept' },
  { affiliate: 'sana.trabelsi@webapp.local', store: 'nour-mode', message: 'Page mode de 15 000 abonnées', decide: 'accept' },
  { affiliate: 'sana.trabelsi@webapp.local', store: 'slim-electronique', message: null },
  { affiliate: 'youssef.mansour@webapp.local', store: 'parfumerie-karim', message: 'Je vends à mes collègues' },
  { affiliate: 'youssef.mansour@webapp.local', store: 'couffin-et-co', message: null, decide: 'end', reason: 'Fin de la collaboration' },
  { affiliate: 'walid.ayari@webapp.local', store: 'nour-mode', message: 'Je débute', decide: 'refuse', reason: 'Profil sans audience pour le moment' },
  { affiliate: 'spam.affiliate@webapp.local', store: 'parfumerie-karim', message: 'Code promo !!!', decide: 'accept' },
];
const ownerTokens = new Map();
async function ownerToken(email) {
  if (!ownerTokens.has(email)) {
    ownerTokens.set(email, (await call('POST', '/api/auth/login', { body: { email, password: PASSWORD } })).data.accessToken);
  }
  return ownerTokens.get(email);
}
for (const p of partnerships) {
  const session = sessions[p.affiliate];
  if (!session || session.alreadyBlocked) continue;
  const store = (await call('GET', `/api/stores/${p.store}`)).data;
  const mine = (await call('GET', '/api/me/partnerships', { token: session.accessToken })).data;
  if (mine.some((m) => m.store.id === store.id)) continue;

  const asked = await call('POST', '/api/me/partnerships', { token: session.accessToken, body: { storeId: store.id, message: p.message } });
  if (asked.status !== 201) throw new Error(`${p.affiliate} → ${p.store}: ${asked.status} ${asked.data?.code ?? ''}`);
  if (p.decide) {
    const token = await ownerToken(stores.find((s) => s.slug === p.store).owner);
    const base = `/api/me/store/partnerships/${asked.data.id}`;
    if (p.decide === 'refuse') await call('POST', `${base}/refuse`, { token, body: { reason: p.reason ?? null } });
    else await call('POST', `${base}/accept`, { token });
    if (p.decide === 'end') await call('POST', `${base}/end`, { token, body: { reason: p.reason ?? null } });
  }
  console.log(`partner  ${p.affiliate} → ${p.store} (${p.decide ?? 'waiting'})`);
}

// 9. Blocked accounts (audited)
for (const u of users.filter((x) => x.blocked && !sessions[x.email].alreadyBlocked)) {
  await call('POST', `/api/admin/users/${sessions[u.email].user.id}/block`, { token: adminToken, body: { reason: u.blocked } });
}

console.log('\n' + sql(`select rpad(full_name, 19) || rpad(email, 34) || rpad(array_to_string(roles, ','), 9) || coalesce(affiliate_code, '      ') || case when status = 2 then '  BLOCKED' else '' end from users order by created_at;`));
console.log(sql(`select rpad(s.name, 20) || rpad(s.slug, 22) || rpad(u.email, 32) || (select count(*) from products p where p.store_id = s.id and p.deleted_at is null) || ' products' from stores s join users u on u.id = s.owner_id order by s.created_at;`));
console.log(sql(`select 'role requests: ' || count(*) filter (where status = 1) || ' pending, ' || count(*) filter (where status = 2) || ' approved, ' || count(*) filter (where status = 3) || ' refused, ' || count(*) filter (where status = 4) || ' withdrawn' from role_requests;`));
console.log(sql(`select 'partnerships: ' || count(*) filter (where status = 1) || ' waiting, ' || count(*) filter (where status = 2) || ' accepted, ' || count(*) filter (where status = 3) || ' refused, ' || count(*) filter (where status = 4) || ' ended' from partnerships;`));
console.log(`All demo accounts use the password defined at the top of ${import.meta.url.split('/').pop()}.`);
