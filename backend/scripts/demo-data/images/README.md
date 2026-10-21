# Demo images

Drop your photos here, then run from `backend/`:

```
node scripts/seed-dev-users.mjs --reset-catalog
```

- Formats: **JPEG, PNG or WebP**, **5 MB max** each (setting `upload.maxImageSizeMb`). No SVG / GIF.
- Any file name inside a product folder works; they are taken in name order (`1.jpg`, `2.jpg`…), **the first one is the main photo**, 8 max.
- Square photos (e.g. 1000 × 1000) look best for products; banners are wide (e.g. 1600 × 500); logos square.
- Missing image = a plain colour tile: add photos little by little and re-run.
- Use photos you own or that are free to use (your own pictures, the store's, free-licence sites like Unsplash / Pexels). Brand / shop photos are fine for local tests only.
- Most photos come from Wikimedia Commons (free licences) — author, licence and source of each are in `CREDITS.md`. Replace any of them by dropping your own file in the folder.
- ✅ = an image is already there.

## Store categories — `store-categories/<name>.jpg`

- ✅ `store-categories/mode.jpg` — Mode & vêtements
- ✅ `store-categories/chaussures.jpg` — Chaussures
- ✅ `store-categories/parfums.jpg` — Parfums
- ✅ `store-categories/beaute.jpg` — Beauté & soins
- ✅ `store-categories/bijoux.jpg` — Bijoux
- ✅ `store-categories/electronique.jpg` — Électronique
- ✅ `store-categories/maison-deco.jpg` — Maison & déco
- ✅ `store-categories/epicerie-fine.jpg` — Épicerie fine
- ✅ `store-categories/patisserie.jpg` — Pâtisserie
- ✅ `store-categories/artisanat.jpg` — Artisanat tunisien

## Stores — `stores/<store>/logo.jpg` and `banner.jpg`

- ✅ logo, ✅ banner — `stores/parfumerie-karim/` — Parfumerie Karim
- ✅ logo, ✅ banner — `stores/nour-mode/` — Nour Mode
- ✅ logo, ✅ banner — `stores/slim-electronique/` — Slim Électronique
- ✅ logo, ✅ banner — `stores/amine-cosmetiques/` — Amine Cosmétiques
- ✅ logo, ✅ banner — `stores/douceurs-d-amel/` — Douceurs d'Amel
- ✅ logo, ✅ banner — `stores/dar-zitouna/` — Dar Zitouna
- ✅ logo, ✅ banner — `stores/fakhar-nabeul/` — Fakhar Nabeul
- ✅ logo, ✅ banner — `stores/fadha-bijoux/` — Fadha Bijoux
- ✅ logo, ✅ banner — `stores/kooli-chaussures/` — Kooli Chaussures
- ✅ logo, ✅ banner — `stores/couffin-et-co/` — Couffin & Co

## Products — `products/<product>/1.jpg`, `2.jpg`…

### Parfumerie Karim

- ✅ 1 `products/oud-royal/` — Armani Privé — Oud Royal, 100 ml
- ✅ 1 `products/oud-al-qasr-100ml/` — Oud Al Qasr — 100 ml
- ✅ 1 `products/musc-blanc-50ml/` — Musc blanc — 50 ml
- ✅ 1 `products/jasmin-de-tunis/` — Jasmin de Tunis — 100 ml
- ✅ 1 `products/fleur-oranger-nabeul-50ml/` — Fleur d'oranger de Nabeul — 50 ml
- ✅ 1 `products/cedre-et-cuir-100ml/` — Cèdre & cuir — 100 ml
- ✅ 1 `products/ambre-nuit/` — Dior — Ambre Nuit, 125 ml
- ✅ 1 `products/brise-marine-100ml/` — Brise marine — 100 ml

### Nour Mode

- ✅ 1 `products/robe-fleurie-d-ete/` — Robe fleurie d'été
- ✅ 1 `products/robe-de-soiree-longue/` — Robe de soirée longue en dentelle
- ✅ 1 `products/t-shirt-basique-coton/` — T-shirt basique coton
- ✅ 1 `products/robe-longue-en-lin/` — Robe longue en lin
- ✅ 1 `products/kaftan-brode-main/` — Kaftan brodé main
- ✅ 1 `products/jebba-femme-en-soie/` — Jebba femme en soie
- ✅ 1 `products/blouse-brodee-tunisienne/` — Blouse brodée tunisienne
- ✅ 1 `products/chemisier-satin/` — Chemisier en satin

### Slim Électronique

- ✅ 1 `products/samsung-galaxy-a55-5g/` — Samsung Galaxy A55 5G
- ✅ 1 `products/xiaomi-redmi-note-13/` — Xiaomi Redmi Note 13
- ✅ 1 `products/ecouteurs-jbl-tune-520bt/` — Casque JBL Tune 520BT
- ✅ 1 `products/enceinte-bluetooth-portable/` — Enceinte Bluetooth portable étanche
- ✅ 1 `products/chargeur-rapide-usb-c-33w/` — Chargeur rapide USB-C 33 W
- ✅ 1 `products/coque-silicone-galaxy-a55/` — Coque silicone Galaxy A55

### Amine Cosmétiques

- ✅ 1 `products/huile-figue-de-barbarie-30ml/` — Huile de figue de Barbarie bio — 30 ml
- ✅ 1 `products/creme-hydratante-argan/` — Crème hydratante à l'huile d'argan — 50 ml
- ✅ 1 `products/savon-noir-eucalyptus/` — Savon noir à l'eucalyptus
- ⬜ `products/argile-tfal-naturelle/` — Argile tfal naturelle
- ✅ 1 `products/huile-ricin-cheveux-100ml/` — Huile de ricin pour cheveux — 100 ml
- ✅ 1 `products/khol-traditionnel/` — Khôl traditionnel

### Douceurs d'Amel

- ✅ 1 `products/baklawa-amandes-500g/` — Baklawa aux amandes — 500 g
- ✅ 1 `products/makroudh-kairouan-500g/` — Makroudh de Kairouan — 500 g
- ✅ 1 `products/kaak-warka-400g/` — Kaak warka — 400 g
- ✅ 1 `products/ghraiba-sorgho-500g/` — Ghraiba au sorgho — 500 g
- ✅ 1 `products/bjawia-500g/` — Bjawia — 500 g
- ✅ 1 `products/gateau-amandes-fleur-oranger/` — Gâteau amandes & fleur d'oranger

### Dar Zitouna

- ✅ 1 `products/huile-olive-extra-vierge-1l/` — Huile d'olive extra vierge — 1 L
- ✅ 1 `products/huile-olive-bio-500ml/` — Huile d'olive bio — 500 ml
- ✅ 1 `products/dattes-deglet-nour-1kg/` — Dattes Deglet Nour — 1 kg
- ✅ 1 `products/dattes-fourrees-amandes-500g/` — Dattes fourrées aux amandes — 500 g
- ✅ 1 `products/harissa-cap-bon-250g/` — Harissa traditionnelle du Cap Bon — 250 g
- ✅ 1 `products/tabel-epices-100g/` — Tabel (mélange d’épices) — 100 g

### Fakhar Nabeul

- ✅ 1 `products/plat-tajine-terre-cuite/` — Plat à tajine en terre cuite
- ✅ 1 `products/couscoussier-aluminium/` — Couscoussier (keskes) 8 L
- ✅ 1 `products/assiettes-peintes-lot-6/` — Assiettes peintes à la main (lot de 6)
- ✅ 1 `products/vase-ceramique-bleu/` — Vase en céramique bleue
- ✅ 1 `products/kilim-berbere-120x180/` — Kilim berbère 120 × 180 cm
- ✅ 1 `products/margoum-kairouan-160x230/` — Margoum de Kairouan 160 × 230 cm

### Fadha Bijoux

- ✅ 1 `products/collier-khomsa-argent/` — Collier khomsa en argent 925
- ✅ 1 `products/collier-poisson-porte-bonheur/` — Collier poisson porte-bonheur
- ✅ 1 `products/bague-berbere-argent/` — Bague berbère en argent
- ✅ 1 `products/boucles-khomsa-plaque-or/` — Boucles d'oreilles khomsa plaqué or
- ✅ 1 `products/bracelet-mahdia-argent/` — Bracelet de Mahdia en argent

### Kooli Chaussures

- ✅ 1 `products/baskets-cuir-blanches/` — Baskets en cuir blanches
- ✅ 1 `products/baskets-running-legeres/` — Baskets running légères
- ✅ 1 `products/sandales-cuir-femme/` — Sandales en cuir femme
- ✅ 1 `products/balgha-cuir-homme/` — Balgha en cuir homme
- ✅ 1 `products/babouches-brodees-femme/` — Babouches brodées femme

### Couffin & Co

- ✅ 1 `products/couffin-palmier-kerkennah/` — Couffin en palmier de Kerkennah
- ✅ 1 `products/panier-rond-tresse/` — Panier rond tressé
- ✅ 1 `products/chapeau-paille-mdhalla/` — Chapeau de paille (mdhalla)
- ✅ 1 `products/plateau-cuivre-cisele/` — Plateau en cuivre ciselé
- ✅ 1 `products/theiere-cuivre/` — Théière en cuivre
