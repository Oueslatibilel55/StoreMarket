// Writes backend/scripts/dev-users.xlsx — the demo accounts of the LOCAL dev database, as a workbook.
// No dependency: the sheets are written as Office Open XML and zipped with node:zlib (Node 24).
//   node backend/scripts/demo-data/write-dev-users-xlsx.mjs
// Edit the data below when the seed changes (affiliate codes change after a database reset).
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { crc32, deflateRawSync } from 'node:zlib';

const PASSWORD = 'Demo2026x';
const out = join(dirname(fileURLToPath(import.meta.url)), '..', 'dev-users.xlsx');

// -- Data ---------------------------------------------------------------------------------------------

const accounts = [
  // [name, email, roles, code, store, situation, try]
  ['Admin Démo', 'admin@webapp.local', 'Buyer, Admin', '', '', 'Client sign-up + first admin (BootstrapAdmin)', 'Admin panel /admin: users, role requests, moderation, reviews (/admin/reviews), payouts'],
  ['Leila Gharbi', 'leila.gharbi@webapp.local', 'Buyer', '', '', 'Client account. Delivered orders CMD-2026-000001 / 000002 (Parfumerie Karim)', 'Cart, checkout, /orders, rate products and the store at /reviews'],
  ['Sana Trabelsi', 'sana.trabelsi@webapp.local', 'Affiliate', '#DPP-54', '', 'Affiliate account (no Buyer role). Partner of Parfumerie Karim and Nour Mode; waiting on Slim Électronique and Dar Zitouna', '/affiliate, wallet, payouts, share links ?ref=DPP-54'],
  ['Karim Ben Salah', 'karim.bensalah@webapp.local', 'Buyer, StoreOwner', '', 'Parfumerie Karim', 'Client sign-up + StoreOwner request approved', '/my-store: products, orders, affiliates, reviews (reply)'],
  ['Ines Chaabane', 'ines.chaabane@webapp.local', 'Buyer', '', '', 'Plain client. Delivered order CMD-2026-000003 (Nour Mode + Douceurs d\'Amel)', 'Rate products of two stores at /reviews'],
  ['Yasmine Ferchichi', 'yasmine.ferchichi@webapp.local', 'Buyer', '', '', 'Asked to be affiliate, then withdrew. CMD-2026-000004 delivered (Parfumerie Karim), CMD-2026-000005 shipped only', 'Delivered order can be rated, the shipped one cannot yet'],
  ['Mehdi Bouazizi', 'mehdi.bouazizi@webapp.local', 'Buyer, Affiliate', '#MZS-73', '', 'Affiliate request approved by the admin', 'Client and affiliate on one account'],
  ['Omar Khelifi', 'omar.khelifi@webapp.local', 'Buyer', '', '', 'StoreOwner request PENDING', 'Approve / refuse it in /admin/role-requests'],
  ['Rania Saidi', 'rania.saidi@webapp.local', 'Buyer', '', '', 'StoreOwner request REFUSED ("Aucune information sur la boutique")', 'Refusal reason shown in My account'],
  ['Fatma Dridi', 'fatma.dridi@webapp.local', 'Buyer, Affiliate', '#LYV-23', '', 'Client, affiliate request approved', ''],
  ['Youssef Mansour', 'youssef.mansour@webapp.local', 'Affiliate', '#BBR-47', '', 'Partner of Parfumerie Karim; partnership with Couffin & Co ended', 'Ended partnership history'],
  ['Walid Ayari', 'walid.ayari@webapp.local', 'Affiliate', '#GCY-48', '', 'Partnership refused by Nour Mode', 'Ask again after a refusal'],
  ['Nesrine Toumi', 'nesrine.toumi@webapp.local', 'Affiliate, Buyer', '#YNM-42', '', 'Affiliate, Buyer request approved', ''],
  ['Hela Jebali', 'hela.jebali@webapp.local', 'Affiliate', '#JYW-22', '', 'Affiliate, StoreOwner request PENDING', 'Approve it, then create her store'],
  ['Amine Jaziri', 'amine.jaziri@webapp.local', 'Affiliate, StoreOwner', '#CWM-95', 'Amine Cosmétiques', 'Affiliate, StoreOwner request approved', 'Affiliate who also owns a store (no self-affiliation)'],
  ['Nour Haddad', 'nour.haddad@webapp.local', 'Buyer, StoreOwner', '', 'Nour Mode', 'Store owner', 'Products with variants (sizes, colours)'],
  ['Slim Ben Amor', 'slim.benamor@webapp.local', 'Buyer, StoreOwner', '', 'Slim Électronique', 'Store owner', 'Storage variants'],
  ['Amel Zouari', 'amel.zouari@webapp.local', 'Buyer, StoreOwner, Affiliate', '#TKQ-21', 'Douceurs d\'Amel', 'Store owner, affiliate request approved. Has 1 shipped order to deliver (CMD-2026-000005)', 'Mark it Delivered, then Yasmine can rate it'],
  ['Mourad Ghanmi', 'mourad.ghanmi@webapp.local', 'Buyer, StoreOwner', '', 'Dar Zitouna', 'Store owner. Delivered guest order CMD-2026-000006', ''],
  ['Riadh Mejri', 'riadh.mejri@webapp.local', 'Buyer, StoreOwner', '', 'Fakhar Nabeul', 'Store owner', ''],
  ['Sonia Ben Youssef', 'sonia.benyoussef@webapp.local', 'Buyer, StoreOwner', '', 'Fadha Bijoux', 'Store owner', 'Ring sizes'],
  ['Ahmed Kooli', 'ahmed.kooli@webapp.local', 'Buyer, StoreOwner', '', 'Kooli Chaussures', 'Store owner', 'Shoe sizes'],
  ['Lamia Hadj Ali', 'lamia.hadjali@webapp.local', 'Buyer, StoreOwner', '', 'Couffin & Co', 'Store owner', ''],
  ['Faux Vendeur', 'fake.seller@webapp.local', 'Buyer', '', '', 'BLOCKED: "Produits contrefaits signalés"', 'Sign-in refused (blocked account)'],
  ['Spam Affilié', 'spam.affiliate@webapp.local', 'Affiliate', '#RUX-26', '', 'BLOCKED: "Faux avis et spam de code affilié"', 'Sign-in refused (blocked account)'],
];

const reviewTests = [
  // [step, account, where, expected]
  ['1. Rate a product and the store', 'leila.gharbi@webapp.local', '/reviews', '3 items left to rate (Leila already rated "Cèdre & cuir" 4★). Publish one: it moves to "Mes avis publiés", editable for 30 days'],
  ['2. Public page', 'no account', '/products/cedre-et-cuir-100ml', 'Stars under the title, "Avis clients" section with the 1★…5★ bars and the store reply'],
  ['3. Two stores in one order', 'ines.chaabane@webapp.local', '/reviews', 'Products of Nour Mode and Douceurs d\'Amel + one "Service de …" line per store'],
  ['4. Not delivered yet', 'yasmine.ferchichi@webapp.local', '/reviews', 'Only CMD-2026-000004 (delivered) is listed; CMD-2026-000005 is shipped, not rateable'],
  ['5. Deliver, then rate', 'amel.zouari@webapp.local → yasmine.ferchichi@webapp.local', '/my-store/orders → /reviews', 'Amel marks CMD-2026-000005 Delivered; Yasmine now sees it in "À noter"'],
  ['6. Guest order + same phone', 'new account, phone 50 777 888', '/register → /reviews', 'Sign up as a client with phone 50 777 888: guest order CMD-2026-000006 (Dar Zitouna) appears to rate'],
  ['7. Store owner reply', 'karim.bensalah@webapp.local', '/my-store/reviews', 'Stats, "Sans réponse" counter, reply / edit / remove the public answer'],
  ['8. Admin moderation', 'admin@webapp.local', '/admin/reviews', 'Hide a review with a reason: gone from the product page and the average; author and owner see "Masqué". Show it again. Both in /admin/audit'],
  ['9. Edit window', 'admin@webapp.local', '/admin/settings', '"Délai pour modifier un avis" (30 days by default) applies to new reviews only'],
  ['10. Top rated', 'no account', '/ and /products?sort=TopRated', 'Home "Les mieux notés" shows rated products; catalogue sort "Mieux notés"'],
];

const stores = [
  ['Parfumerie Karim', 'parfumerie-karim', 'karim.bensalah@webapp.local', 'Parfums (Armani Privé Oud Royal, Dior Ambre Nuit, house creations)'],
  ['Nour Mode', 'nour-mode', 'nour.haddad@webapp.local', 'Mode (dresses, kaftan, jebba, men\'s T-shirt — variants)'],
  ['Slim Électronique', 'slim-electronique', 'slim.benamor@webapp.local', 'Électronique (Galaxy A55, Redmi Note 13 — storage variants)'],
  ['Amine Cosmétiques', 'amine-cosmetiques', 'amine.jaziri@webapp.local', 'Beauté (figue de Barbarie, tfal, savon noir…)'],
  ['Douceurs d\'Amel', 'douceurs-d-amel', 'amel.zouari@webapp.local', 'Pâtisserie (baklawa, makroudh, kaak warka…)'],
  ['Dar Zitouna', 'dar-zitouna', 'mourad.ghanmi@webapp.local', 'Épicerie fine (huile d\'olive, dattes, harissa)'],
  ['Fakhar Nabeul', 'fakhar-nabeul', 'riadh.mejri@webapp.local', 'Maison & déco (poterie, kilim, margoum)'],
  ['Fadha Bijoux', 'fadha-bijoux', 'sonia.benyoussef@webapp.local', 'Bijoux (khomsa, bague berbère — ring sizes)'],
  ['Kooli Chaussures', 'kooli-chaussures', 'ahmed.kooli@webapp.local', 'Chaussures (baskets, sandales, balghas — sizes)'],
  ['Couffin & Co', 'couffin-et-co', 'lamia.hadjali@webapp.local', 'Artisanat (couffins, cuivre)'],
];

const notes = [
  ['DEMO DATA — LOCAL DEV DATABASE ONLY. Never use these accounts or this password outside your machine.'],
  [''],
  ['App', 'http://localhost:4200'],
  ['Password (every account)', PASSWORD],
  ['Created by', 'node scripts/seed-dev-users.mjs (from backend/, API running) + the review test orders CMD-2026-000002…000006'],
  ['This file', 'node scripts/demo-data/write-dev-users-xlsx.mjs (edit the data in that script)'],
  [''],
  ['Role requests', '2 pending (Omar, Hela), 14 approved, 1 refused, 1 withdrawn → /admin/role-requests'],
  ['Extra roles', 'Never self-service: "My account" > "Add a role", then approve / refuse as admin in /admin/role-requests'],
  ['Catalogue', '10 stores, 61 products, 67 variants, 42 product categories, 10 store categories — source scripts/demo-data/catalog.mjs'],
  ['Affiliate codes', 'Generated randomly: after a database reset the seed prints new ones (update this file)'],
  [''],
  ['Reset the catalogue only', 'node scripts/seed-dev-users.mjs --reset-catalog'],
  ['Full reset (local DB)', 'docker exec -e PGPASSWORD=webapp_dev webapp-postgres-aspire psql -U webapp -d webapp -c "truncate product_variants, products, product_attributes, product_categories, stores, store_categories, images, platform_settings, role_requests, refresh_tokens, audit_entries, users;"  then  node scripts/seed-dev-users.mjs --reset-catalog'],
];

// -- Sheets -------------------------------------------------------------------------------------------

const sheets = [
  {
    name: 'Accounts',
    widths: [20, 32, 12, 28, 10, 20, 60, 60],
    header: ['Name', 'Email', 'Password', 'Roles', 'Code', 'Store', 'Situation', 'Good for testing'],
    rows: accounts.map(([n, e, r, c, s, sit, t]) => [n, e, PASSWORD, r, c, s, sit, t]),
  },
  { name: 'Reviews tests (017)', widths: [30, 38, 32, 90], header: ['Scenario', 'Account', 'Page', 'Expected'], rows: reviewTests },
  { name: 'Stores', widths: [20, 20, 32, 62], header: ['Store', 'URL name (/stores/…)', 'Owner', 'Category / products'], rows: stores },
  { name: 'Notes', widths: [26, 120], header: null, rows: notes },
];

// -- Office Open XML ----------------------------------------------------------------------------------

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const col = (i) => { let s = ''; for (i++; i; i = Math.floor((i - 1) / 26)) s = String.fromCharCode(65 + ((i - 1) % 26)) + s; return s; };
const cell = (ref, value, style) => `<c r="${ref}" t="inlineStr" s="${style}"><is><t xml:space="preserve">${esc(value)}</t></is></c>`;

function sheetXml({ widths, header, rows }) {
  const all = header ? [header, ...rows] : rows;
  const body = all.map((row, r) => `<row r="${r + 1}">${row.map((v, c) => cell(`${col(c)}${r + 1}`, v, header && r === 0 ? 1 : 2)).join('')}</row>`).join('');
  const last = `${col(widths.length - 1)}${all.length}`;
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<sheetViews><sheetView workbookViewId="0">${header ? '<pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>' : ''}</sheetView></sheetViews>
<cols>${widths.map((w, i) => `<col min="${i + 1}" max="${i + 1}" width="${w}" customWidth="1"/>`).join('')}</cols>
<sheetData>${body}</sheetData>${header ? `<autoFilter ref="A1:${last}"/>` : ''}
</worksheet>`;
}

const styles = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<fonts count="2"><font><sz val="10"/><name val="Arial"/></font><font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="Arial"/></font></fonts>
<fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF4F46E5"/></patternFill></fill></fills>
<borders count="2"><border/><border><bottom style="thin"><color rgb="FFE2E8F0"/></bottom></border></borders>
<cellStyleXfs count="1"><xf/></cellStyleXfs>
<cellXfs count="3">
<xf fontId="0" fillId="0" borderId="0"/>
<xf fontId="1" fillId="2" borderId="0" applyFont="1" applyFill="1"><alignment vertical="center"/></xf>
<xf fontId="0" fillId="0" borderId="1" applyBorder="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
</cellXfs>
</styleSheet>`;

const files = {
  '[Content_Types].xml': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
${sheets.map((_, i) => `<Override PartName="/xl/worksheets/sheet${i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`).join('\n')}
</Types>`,
  '_rels/.rels': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>`,
  'xl/workbook.xml': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheets>${sheets.map((s, i) => `<sheet name="${esc(s.name)}" sheetId="${i + 1}" r:id="rId${i + 1}"/>`).join('')}</sheets>
<definedNames>${sheets.map((s, i) => (s.header ? `<definedName name="_xlnm._FilterDatabase" localSheetId="${i}" hidden="1">'${esc(s.name)}'!$A$1:$${col(s.widths.length - 1)}$${s.rows.length + 1}</definedName>` : '')).join('')}</definedNames>
</workbook>`,
  'xl/_rels/workbook.xml.rels': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
${sheets.map((_, i) => `<Relationship Id="rId${i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${i + 1}.xml"/>`).join('\n')}
<Relationship Id="rId${sheets.length + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>`,
  'xl/styles.xml': styles,
  ...Object.fromEntries(sheets.map((s, i) => [`xl/worksheets/sheet${i + 1}.xml`, sheetXml(s)])),
};

// -- Zip (deflate, no external tool) ------------------------------------------------------------------

function zip(entries) {
  const local = [];
  const central = [];
  let offset = 0;
  for (const [name, text] of Object.entries(entries)) {
    const data = Buffer.from(text, 'utf8');
    const packed = deflateRawSync(data);
    const nameBuf = Buffer.from(name, 'utf8');
    const crc = crc32(data);
    const head = Buffer.alloc(30);
    head.writeUInt32LE(0x04034b50, 0); head.writeUInt16LE(20, 4); head.writeUInt16LE(0x0800, 6); head.writeUInt16LE(8, 8);
    head.writeUInt32LE(crc, 14); head.writeUInt32LE(packed.length, 18); head.writeUInt32LE(data.length, 22); head.writeUInt16LE(nameBuf.length, 26);
    local.push(head, nameBuf, packed);
    const dir = Buffer.alloc(46);
    dir.writeUInt32LE(0x02014b50, 0); dir.writeUInt16LE(20, 4); dir.writeUInt16LE(20, 6); dir.writeUInt16LE(0x0800, 8); dir.writeUInt16LE(8, 10);
    dir.writeUInt32LE(crc, 16); dir.writeUInt32LE(packed.length, 20); dir.writeUInt32LE(data.length, 24); dir.writeUInt16LE(nameBuf.length, 28);
    dir.writeUInt32LE(offset, 42);
    central.push(dir, nameBuf);
    offset += head.length + nameBuf.length + packed.length;
  }
  const dirSize = central.reduce((n, b) => n + b.length, 0);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0); end.writeUInt16LE(central.length / 2, 8); end.writeUInt16LE(central.length / 2, 10);
  end.writeUInt32LE(dirSize, 12); end.writeUInt32LE(offset, 16);
  return Buffer.concat([...local, ...central, end]);
}

writeFileSync(out, zip(files));
console.log(`wrote ${out} (${accounts.length} accounts, ${reviewTests.length} review scenarios, ${stores.length} stores)`);
