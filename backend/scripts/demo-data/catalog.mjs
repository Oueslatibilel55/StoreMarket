// Demo catalogue of the LOCAL dev database: store categories, product category tree (with fields),
// stores and their products. Read by ../seed-dev-users.mjs. Edit freely — then re-run the seed with
// --reset-catalog to rebuild the catalogue from this file.
//
// Images: put them in ./images (see ./images/README.md for the exact names). Anything without an
// image gets a plain colour tile.
//
// Keys: field keys and choice keys are made from the French names by the API (accents removed,
// lower case, dashes): "Eau de parfum" → eau-de-parfum, "64 Go" → 64-go. Product values use them.

/** A field of a product category. Choices: French names, or [fr, ar, en]. */
const field = (fr, type, { ar = null, en = null, unit = null, required = false, filterable = false, options } = {}) => ({
  key: null, nameFr: fr, nameAr: ar, nameEn: en, type, unit, required, filterable,
  options: (options ?? []).map((o) => (Array.isArray(o)
    ? { key: null, name: { fr: o[0], ar: o[1] ?? null, en: o[2] ?? null } }
    : { key: null, name: { fr: o, ar: null, en: null } })),
});

const colours = [
  ['Noir', 'أسود', 'Black'], ['Blanc', 'أبيض', 'White'], ['Beige', 'بيج', 'Beige'], ['Rouge', 'أحمر', 'Red'],
  ['Bleu', 'أزرق', 'Blue'], ['Vert', 'أخضر', 'Green'], ['Marron', 'بني', 'Brown'], ['Rose', 'وردي', 'Pink'],
  ['Gris', 'رمادي', 'Grey'],
];
const sizes = ['XS', 'S', 'M', 'L', 'XL', 'XXL'];

// -- Store categories -----------------------------------------------------------------------------
export const storeCategories = [
  { slug: 'mode', nameFr: 'Mode & vêtements', nameAr: 'أزياء وملابس', nameEn: 'Fashion', icon: '👗', color: [99, 102, 241] },
  { slug: 'chaussures', nameFr: 'Chaussures', nameAr: 'أحذية', nameEn: 'Shoes', icon: '👟', color: [14, 165, 233] },
  { slug: 'parfums', nameFr: 'Parfums', nameAr: 'عطور', nameEn: 'Perfumes', icon: '🌸', color: [236, 72, 153] },
  { slug: 'beaute', nameFr: 'Beauté & soins', nameAr: 'جمال وعناية', nameEn: 'Beauty & care', icon: '💄', color: [244, 63, 94] },
  { slug: 'bijoux', nameFr: 'Bijoux', nameAr: 'مجوهرات', nameEn: 'Jewellery', icon: '💍', color: [234, 179, 8] },
  { slug: 'electronique', nameFr: 'Électronique', nameAr: 'إلكترونيات', nameEn: 'Electronics', icon: '📱', color: [100, 116, 139] },
  { slug: 'maison-deco', nameFr: 'Maison & déco', nameAr: 'المنزل والديكور', nameEn: 'Home & decor', icon: '🏺', color: [34, 197, 94] },
  { slug: 'epicerie-fine', nameFr: 'Épicerie fine', nameAr: 'بقالة فاخرة', nameEn: 'Fine food', icon: '🫒', color: [101, 163, 13] },
  { slug: 'patisserie', nameFr: 'Pâtisserie', nameAr: 'حلويات', nameEn: 'Pastry', icon: '🍰', color: [249, 115, 22] },
  { slug: 'artisanat', nameFr: 'Artisanat tunisien', nameAr: 'صناعات تقليدية', nameEn: 'Tunisian crafts', icon: '🧺', color: [180, 83, 9] },
];

// -- Product category tree (≤ 3 levels; children inherit the fields of their parents) -------------
export const productTree = [
  { slug: 'mode-femme', fr: 'Mode femme', ar: 'أزياء نسائية', en: "Women's fashion", attrs: [
    field('Taille', 'SingleChoice', { ar: 'المقاس', en: 'Size', required: true, filterable: true, options: sizes }),
    field('Couleur', 'SingleChoice', { ar: 'اللون', en: 'Colour', filterable: true, options: colours }),
    field('Matière', 'Text', { ar: 'الخامة', en: 'Material' }),
  ], children: [
    { slug: 'robes', fr: 'Robes', ar: 'فساتين', en: 'Dresses', attrs: [
      field('Longueur', 'SingleChoice', { ar: 'الطول', en: 'Length', filterable: true, options: [['Courte', 'قصير', 'Short'], ['Midi', 'متوسط', 'Midi'], ['Longue', 'طويل', 'Long']] }),
    ] },
    { slug: 'hauts-chemisiers', fr: 'Hauts & chemisiers', ar: 'قمصان وبلوزات', en: 'Tops & blouses', attrs: [
      field('Manches', 'SingleChoice', { ar: 'الأكمام', en: 'Sleeves', options: [['Courtes', 'قصيرة', 'Short'], ['Longues', 'طويلة', 'Long'], ['Sans manches', 'بدون أكمام', 'Sleeveless']] }),
    ] },
    { slug: 'tenues-traditionnelles', fr: 'Tenues traditionnelles', ar: 'لباس تقليدي', en: 'Traditional wear', attrs: [
      field('Occasion', 'MultiChoice', { ar: 'المناسبة', en: 'Occasion', filterable: true, options: [['Mariage', 'عرس', 'Wedding'], ['Fête', 'عيد', 'Celebration'], ['Quotidien', 'يومي', 'Everyday']] }),
    ] },
  ] },
  { slug: 'mode-homme', fr: 'Mode homme', ar: 'أزياء رجالية', en: "Men's fashion", attrs: [
    field('Taille', 'SingleChoice', { ar: 'المقاس', en: 'Size', required: true, filterable: true, options: sizes }),
    field('Couleur', 'SingleChoice', { ar: 'اللون', en: 'Colour', filterable: true, options: colours }),
    field('Matière', 'Text', { ar: 'الخامة', en: 'Material' }),
  ], children: [
    { slug: 't-shirts-polos', fr: 'T-shirts & polos', ar: 'تيشيرتات وبولو', en: 'T-shirts & polos', attrs: [
      field('Coupe', 'SingleChoice', { ar: 'القصّة', en: 'Fit', options: [['Slim', 'ضيّق', 'Slim'], ['Regular', 'عادي', 'Regular'], ['Oversize', 'واسع', 'Oversize']] }),
    ] },
  ] },
  { slug: 'chaussures', fr: 'Chaussures', ar: 'أحذية', en: 'Shoes', attrs: [
    field('Pointure', 'SingleChoice', { ar: 'المقاس', en: 'Size', required: true, filterable: true, options: ['36', '37', '38', '39', '40', '41', '42', '43', '44', '45'] }),
    field('Couleur', 'SingleChoice', { ar: 'اللون', en: 'Colour', filterable: true, options: colours }),
  ], children: [
    { slug: 'baskets', fr: 'Baskets', ar: 'أحذية رياضية', en: 'Sneakers', attrs: [] },
    { slug: 'sandales', fr: 'Sandales', ar: 'صنادل', en: 'Sandals', attrs: [
      field('Matière', 'SingleChoice', { ar: 'الخامة', en: 'Material', options: [['Cuir', 'جلد', 'Leather'], ['Synthétique', 'اصطناعي', 'Synthetic']] }),
    ] },
    { slug: 'babouches-balghas', fr: 'Babouches & balghas', ar: 'بلغة وشلاكة', en: 'Babouches', attrs: [
      field('Matière', 'SingleChoice', { ar: 'الخامة', en: 'Material', options: [['Cuir', 'جلد', 'Leather'], ['Daim', 'شمواه', 'Suede']] }),
    ] },
  ] },
  { slug: 'parfums', fr: 'Parfums', ar: 'عطور', en: 'Perfumes', attrs: [
    field('Contenance', 'Number', { ar: 'السعة', en: 'Size', unit: 'ml', required: true, filterable: true }),
    field('Genre', 'SingleChoice', { ar: 'الجنس', en: 'Gender', filterable: true, options: [['Homme', 'رجالي', 'Men'], ['Femme', 'نسائي', 'Women'], ['Unisexe', 'للجنسين', 'Unisex']] }),
    field('Concentration', 'SingleChoice', { ar: 'التركيز', en: 'Concentration', options: [['Eau de toilette', 'أو دو تواليت', 'Eau de toilette'], ['Eau de parfum', 'أو دو برفان', 'Eau de parfum'], ['Extrait de parfum', 'خلاصة عطر', 'Extrait de parfum']] }),
    field('Famille olfactive', 'MultiChoice', { ar: 'العائلة العطرية', en: 'Scent family', filterable: true, options: [['Boisé', 'خشبي', 'Woody'], ['Floral', 'زهري', 'Floral'], ['Oriental', 'شرقي', 'Oriental'], ['Frais', 'منعش', 'Fresh'], ['Musqué', 'مسكي', 'Musky']] }),
  ], children: [
    { slug: 'parfums-femme', fr: 'Parfums femme', ar: 'عطور نسائية', en: "Women's perfumes", attrs: [] },
    { slug: 'parfums-homme', fr: 'Parfums homme', ar: 'عطور رجالية', en: "Men's perfumes", attrs: [] },
    { slug: 'oud-musc', fr: 'Oud & musc', ar: 'عود ومسك', en: 'Oud & musk', attrs: [] },
  ] },
  { slug: 'beaute', fr: 'Beauté & soins', ar: 'جمال وعناية', en: 'Beauty & care', attrs: [
    field('Contenance', 'Number', { ar: 'السعة', en: 'Size', unit: 'ml' }),
    field('Bio', 'Boolean', { ar: 'عضوي', en: 'Organic', filterable: true }),
  ], children: [
    { slug: 'maquillage', fr: 'Maquillage', ar: 'مكياج', en: 'Make-up', attrs: [field('Teinte', 'Text', { ar: 'اللون', en: 'Shade' })] },
    { slug: 'soins-visage', fr: 'Soins visage', ar: 'العناية بالوجه', en: 'Face care', attrs: [
      field('Type de peau', 'MultiChoice', { ar: 'نوع البشرة', en: 'Skin type', filterable: true, options: [['Sèche', 'جافة', 'Dry'], ['Grasse', 'دهنية', 'Oily'], ['Mixte', 'مختلطة', 'Combination'], ['Normale', 'عادية', 'Normal'], ['Sensible', 'حساسة', 'Sensitive']] }),
    ] },
    { slug: 'soins-cheveux', fr: 'Soins cheveux', ar: 'العناية بالشعر', en: 'Hair care', attrs: [
      field('Type de cheveux', 'MultiChoice', { ar: 'نوع الشعر', en: 'Hair type', filterable: true, options: [['Secs', 'جاف', 'Dry'], ['Gras', 'دهني', 'Oily'], ['Bouclés', 'مجعد', 'Curly'], ['Normaux', 'عادي', 'Normal']] }),
    ] },
    { slug: 'hammam-savons', fr: 'Hammam & savons', ar: 'حمّام وصابون', en: 'Hammam & soaps', attrs: [field('Ingrédients', 'Text', { ar: 'المكونات', en: 'Ingredients' })] },
  ] },
  { slug: 'bijoux', fr: 'Bijoux', ar: 'مجوهرات', en: 'Jewellery', attrs: [
    field('Métal', 'SingleChoice', { ar: 'المعدن', en: 'Metal', filterable: true, options: [['Or 18 carats', 'ذهب عيار 18', '18-carat gold'], ['Argent 925', 'فضة 925', 'Sterling silver'], ['Plaqué or', 'مطلي بالذهب', 'Gold-plated'], ['Laiton', 'نحاس أصفر', 'Brass']] }),
  ], children: [
    { slug: 'colliers', fr: 'Colliers', ar: 'قلائد', en: 'Necklaces', attrs: [field('Longueur de chaîne', 'Number', { ar: 'طول السلسلة', en: 'Chain length', unit: 'cm' })] },
    { slug: 'bagues', fr: 'Bagues', ar: 'خواتم', en: 'Rings', attrs: [
      field('Taille de bague', 'SingleChoice', { ar: 'مقاس الخاتم', en: 'Ring size', required: true, options: ['50', '52', '54', '56', '58', '60'] }),
    ] },
    { slug: 'boucles-oreilles', fr: "Boucles d'oreilles", ar: 'أقراط', en: 'Earrings', attrs: [] },
    { slug: 'bracelets', fr: 'Bracelets', ar: 'أساور', en: 'Bracelets', attrs: [] },
  ] },
  { slug: 'electronique', fr: 'Électronique', ar: 'إلكترونيات', en: 'Electronics', attrs: [
    field('Marque', 'Text', { ar: 'العلامة', en: 'Brand', required: true, filterable: true }),
    field('Garantie', 'Number', { ar: 'الضمان', en: 'Warranty', unit: 'mois' }),
  ], children: [
    { slug: 'smartphones', fr: 'Smartphones', ar: 'هواتف ذكية', en: 'Smartphones', attrs: [
      field('Stockage', 'SingleChoice', { ar: 'التخزين', en: 'Storage', required: true, filterable: true, options: ['64 Go', '128 Go', '256 Go', '512 Go'] }),
      field('RAM', 'Number', { ar: 'الذاكرة', en: 'RAM', unit: 'Go' }),
      field('Couleur', 'SingleChoice', { ar: 'اللون', en: 'Colour', options: [['Noir', 'أسود', 'Black'], ['Bleu', 'أزرق', 'Blue'], ['Vert', 'أخضر', 'Green'], ['Gris', 'رمادي', 'Grey']] }),
    ] },
    { slug: 'audio', fr: 'Écouteurs & audio', ar: 'سماعات وصوتيات', en: 'Headphones & audio', attrs: [
      field('Sans fil', 'Boolean', { ar: 'لاسلكي', en: 'Wireless', filterable: true }),
      field('Autonomie', 'Number', { ar: 'مدة البطارية', en: 'Battery life', unit: 'h' }),
    ] },
    { slug: 'accessoires-telephone', fr: 'Accessoires téléphone', ar: 'إكسسوارات الهاتف', en: 'Phone accessories', attrs: [
      field('Compatibilité', 'Text', { ar: 'التوافق', en: 'Compatibility' }),
    ] },
  ] },
  { slug: 'maison', fr: 'Maison & déco', ar: 'المنزل والديكور', en: 'Home & decor', attrs: [
    field('Dimensions', 'Text', { ar: 'المقاسات', en: 'Dimensions' }),
    field('Fait main', 'Boolean', { ar: 'صنع يدوي', en: 'Handmade', filterable: true }),
  ], children: [
    { slug: 'poterie-ceramique', fr: 'Poterie & céramique', ar: 'فخار وخزف', en: 'Pottery & ceramics', attrs: [] },
    { slug: 'tapis-kilims', fr: 'Tapis & kilims', ar: 'زرابي ومرقوم', en: 'Rugs & kilims', attrs: [
      field('Matière', 'SingleChoice', { ar: 'الخامة', en: 'Material', options: [['Laine', 'صوف', 'Wool'], ['Coton', 'قطن', 'Cotton']] }),
    ] },
    { slug: 'cuisine', fr: 'Cuisine & arts de la table', ar: 'المطبخ والمائدة', en: 'Kitchen & table', attrs: [
      field('Capacité', 'Number', { ar: 'السعة', en: 'Capacity', unit: 'L' }),
    ] },
  ] },
  { slug: 'epicerie-fine', fr: 'Épicerie fine', ar: 'بقالة فاخرة', en: 'Fine food', attrs: [
    field('Origine', 'Text', { ar: 'المنشأ', en: 'Origin', filterable: true }),
    field('Bio', 'Boolean', { ar: 'عضوي', en: 'Organic', filterable: true }),
  ], children: [
    { slug: 'huile-olive', fr: "Huile d'olive", ar: 'زيت زيتون', en: 'Olive oil', attrs: [
      field('Contenance', 'Number', { ar: 'السعة', en: 'Volume', unit: 'ml', required: true }),
      field('Qualité', 'SingleChoice', { ar: 'الجودة', en: 'Grade', filterable: true, options: [['Extra vierge', 'بكر ممتاز', 'Extra virgin'], ['Vierge', 'بكر', 'Virgin']] }),
    ] },
    { slug: 'dattes', fr: 'Dattes', ar: 'تمور', en: 'Dates', attrs: [
      field('Poids', 'Number', { ar: 'الوزن', en: 'Weight', unit: 'g', required: true }),
      field('Variété', 'SingleChoice', { ar: 'الصنف', en: 'Variety', filterable: true, options: [['Deglet Nour', 'دقلة نور', 'Deglet Nour'], ['Alig', 'عليق', 'Alig'], ['Kentichi', 'كنتيشي', 'Kentichi']] }),
    ] },
    { slug: 'epices-harissa', fr: 'Épices & harissa', ar: 'توابل وهريسة', en: 'Spices & harissa', attrs: [
      field('Poids', 'Number', { ar: 'الوزن', en: 'Weight', unit: 'g', required: true }),
    ] },
  ] },
  { slug: 'patisserie', fr: 'Pâtisserie', ar: 'حلويات', en: 'Pastry', attrs: [
    field('Poids', 'Number', { ar: 'الوزن', en: 'Weight', unit: 'g', required: true }),
    field('Sans gluten', 'Boolean', { ar: 'خالٍ من الغلوتين', en: 'Gluten-free', filterable: true }),
  ], children: [
    { slug: 'patisserie-tunisienne', fr: 'Pâtisserie tunisienne', ar: 'حلويات تونسية', en: 'Tunisian pastries', attrs: [] },
    { slug: 'gateaux', fr: 'Gâteaux', ar: 'كعك وتورتات', en: 'Cakes', attrs: [field('Parts', 'Number', { ar: 'عدد الحصص', en: 'Servings' })] },
  ] },
  { slug: 'artisanat', fr: 'Artisanat', ar: 'صناعات تقليدية', en: 'Crafts', attrs: [
    field('Région', 'Text', { ar: 'الجهة', en: 'Region', filterable: true }),
    field('Fait main', 'Boolean', { ar: 'صنع يدوي', en: 'Handmade', filterable: true }),
  ], children: [
    { slug: 'vannerie', fr: 'Vannerie & couffins', ar: 'سعف وقفاف', en: 'Baskets & weaving', attrs: [] },
    { slug: 'cuivre', fr: 'Cuivre & dinanderie', ar: 'نحاس', en: 'Copperware', attrs: [] },
  ] },
];

// -- New store owners (on top of the accounts already in the seed) --------------------------------
export const storeOwners = [
  { email: 'mourad.ghanmi@webapp.local', fullName: 'Mourad Ghanmi', phone: '74 220 118' },
  { email: 'riadh.mejri@webapp.local', fullName: 'Riadh Mejri', phone: '72 285 640' },
  { email: 'sonia.benyoussef@webapp.local', fullName: 'Sonia Ben Youssef', phone: '98 640 233' },
  { email: 'ahmed.kooli@webapp.local', fullName: 'Ahmed Kooli', phone: '74 401 977' },
  { email: 'lamia.hadjali@webapp.local', fullName: 'Lamia Hadj Ali', phone: '74 489 320' },
];

/**
 * Stores and their products. Product `values`: { fieldKey: value } — text, number, boolean, or
 * choice key(s) (string or array). `variants`: { axes: [fieldKeys], rows: [[optionKey…, stock, price?]] }.
 * `slug`s are given so the images folder names are known in advance.
 */
export const stores = [
  {
    owner: 'karim.bensalah@webapp.local', slug: 'parfumerie-karim', name: 'Parfumerie Karim', category: 'parfums', city: 'sousse',
    cities: ['sousse', 'monastir', 'mahdia', 'tunis', 'ariana', 'nabeul'], percent: 15, fee: 7, days: [1, 3], color: [190, 24, 93],
    description: 'Parfumerie familiale à Sousse depuis 1998 : créations orientales, ouds et eaux fraîches, flacons remplis à la commande.',
    products: [
      { slug: 'oud-royal', name: 'Armani Privé — Oud Royal, 100 ml', category: 'oud-musc', price: 1150, compare: 1290, stock: 3, percent: 8, color: [24, 24, 27],
        description: 'Oud boisé, rose et ambre, de la collection Armani Privé. Flacon authentique, facture fournie.',
        values: { contenance: 100, genre: 'unisexe', concentration: 'eau-de-parfum', 'famille-olfactive': ['oriental', 'boise'] } },
      { slug: 'oud-al-qasr-100ml', name: 'Oud Al Qasr — 100 ml', category: 'oud-musc', price: 145, compare: 170, stock: 12, percent: 20, color: [120, 53, 15],
        description: 'Création de la maison : oud fumé, safran et rose de Damas. Tenue plus de 10 heures.',
        values: { contenance: 100, genre: 'unisexe', concentration: 'extrait-de-parfum', 'famille-olfactive': ['oriental', 'boise'] } },
      { slug: 'musc-blanc-50ml', name: 'Musc blanc — 50 ml', category: 'oud-musc', price: 45, stock: 40, color: [241, 245, 249],
        description: 'Musc blanc doux et poudré, à porter au quotidien.',
        values: { contenance: 50, genre: 'unisexe', concentration: 'eau-de-parfum', 'famille-olfactive': ['musque'] } },
      { slug: 'jasmin-de-tunis', name: 'Jasmin de Tunis — 100 ml', category: 'parfums-femme', price: 89, stock: 25, color: [253, 224, 71],
        description: 'Création de la maison : le jasmin tunisien (machmoum) cueilli à l’aube, sur un fond de musc blanc.',
        values: { contenance: 100, genre: 'femme', concentration: 'eau-de-parfum', 'famille-olfactive': ['floral'] } },
      { slug: 'fleur-oranger-nabeul-50ml', name: "Fleur d'oranger de Nabeul — 50 ml", category: 'parfums-femme', price: 69, stock: 18, color: [254, 215, 170],
        description: 'Néroli du Cap Bon distillé à Nabeul, frais et lumineux.',
        values: { contenance: 50, genre: 'femme', concentration: 'eau-de-toilette', 'famille-olfactive': ['floral', 'frais'] } },
      { slug: 'cedre-et-cuir-100ml', name: 'Cèdre & cuir — 100 ml', category: 'parfums-homme', price: 99, stock: 15, color: [87, 83, 78],
        description: 'Cèdre de l’Atlas, cuir et poivre noir. Boisé et élégant.',
        values: { contenance: 100, genre: 'homme', concentration: 'eau-de-parfum', 'famille-olfactive': ['boise'] } },
      { slug: 'ambre-nuit', name: 'Dior — Ambre Nuit, 125 ml', category: 'parfums', price: 980, stock: 2, percent: 8, color: [154, 52, 18],
        description: 'Ambre, rose et bois, de la Collection Privée Christian Dior. Flacon authentique, facture fournie.',
        values: { contenance: 125, genre: 'unisexe', concentration: 'eau-de-parfum', 'famille-olfactive': ['oriental', 'boise'] } },
      { slug: 'brise-marine-100ml', name: 'Brise marine — 100 ml', category: 'parfums-homme', price: 75, stock: 0, color: [56, 189, 248],
        description: 'Création de la maison : notes marines et agrumes, idéal pour l’été. De retour bientôt.',
        values: { contenance: 100, genre: 'homme', concentration: 'eau-de-toilette', 'famille-olfactive': ['frais'] } },
    ],
  },
  {
    owner: 'nour.haddad@webapp.local', slug: 'nour-mode', name: 'Nour Mode', category: 'mode', city: 'tunis',
    cities: ['tunis', 'ariana', 'ben-arous', 'manouba', 'nabeul', 'sousse'], percent: 12, fee: 7, days: [1, 2], color: [79, 70, 229],
    description: 'Prêt-à-porter féminin, robes de soirée, tenues traditionnelles revisitées et basiques homme.',
    products: [
      { slug: 'robe-fleurie-d-ete', name: "Robe fleurie d'été", category: 'robes', price: 119, compare: 149, color: [244, 114, 182],
        description: 'Robe courte épaules dénudées, volants et imprimé fleuri bleu et ocre, viscose légère.',
        values: { couleur: 'beige', matiere: 'Viscose', longueur: 'courte' }, variants: { axes: ['taille'], rows: [['xs', 2], ['s', 4], ['m', 6], ['l', 3], ['xl', 0]] } },
      { slug: 'robe-de-soiree-longue', name: 'Robe de soirée longue en dentelle', category: 'robes', price: 289, percent: 10, color: [24, 24, 27],
        description: 'Dentelle noire brodée de sequins, manches longues, épaules dénudées, ceinture en satin.',
        values: { couleur: 'noir', matiere: 'Dentelle et satin', longueur: 'longue' }, variants: { axes: ['taille'], rows: [['m', 2], ['l', 3], ['xl', 2], ['xxl', 1]] } },
      { slug: 't-shirt-basique-coton', name: 'T-shirt basique coton', category: 't-shirts-polos', price: 35, percent: 15, color: [107, 114, 128],
        description: 'T-shirt homme en coton peigné, col rond, poche poitrine.',
        values: { matiere: 'Coton 100 %', coupe: 'regular' },
        variants: { axes: ['taille', 'couleur'], rows: [['s', 'gris', 6], ['m', 'gris', 10], ['l', 'gris', 8], ['xl', 'gris', 4], ['m', 'noir', 7], ['l', 'noir', 5], ['m', 'blanc', 0]] } },
      { slug: 'robe-longue-en-lin', name: 'Robe longue en lin', category: 'robes', price: 159, color: [231, 229, 228],
        description: 'Lin lavé, coupe ample, poches. Fraîche pour les journées chaudes.',
        values: { matiere: 'Lin', longueur: 'longue' },
        variants: { axes: ['taille', 'couleur'], rows: [['s', 'beige', 3], ['m', 'beige', 5], ['l', 'beige', 2], ['s', 'blanc', 2], ['m', 'blanc', 4], ['l', 'blanc', 0]] } },
      { slug: 'kaftan-brode-main', name: 'Kaftan brodé main', category: 'tenues-traditionnelles', price: 390, percent: 10, color: [30, 64, 175],
        description: 'Kaftan en velours brodé de fils dorés, finitions faites à la main.',
        values: { couleur: 'bleu', matiere: 'Velours', occasion: ['mariage', 'fete'] }, variants: { axes: ['taille'], rows: [['s', 1], ['m', 2], ['l', 1]] } },
      { slug: 'jebba-femme-en-soie', name: 'Jebba femme en soie', category: 'tenues-traditionnelles', price: 280, stock: 3, color: [253, 230, 138],
        description: 'Jebba légère en soie naturelle, broderies ton sur ton.',
        values: { taille: 'm', couleur: 'beige', matiere: 'Soie', occasion: ['fete', 'quotidien'] } },
      { slug: 'blouse-brodee-tunisienne', name: 'Blouse brodée tunisienne', category: 'hauts-chemisiers', price: 79, color: [255, 255, 255],
        description: 'Coton léger, broderie traditionnelle au col et aux manches.',
        values: { couleur: 'blanc', matiere: 'Coton', manches: 'longues' }, variants: { axes: ['taille'], rows: [['s', 5], ['m', 8], ['l', 4]] } },
      { slug: 'chemisier-satin', name: 'Chemisier en satin', category: 'hauts-chemisiers', price: 69, color: [127, 29, 29],
        description: 'Satin fluide, boutons nacrés.',
        values: { matiere: 'Satin', manches: 'longues' }, variants: { axes: ['taille', 'couleur'], rows: [['s', 'noir', 4], ['m', 'noir', 6], ['s', 'rouge', 2], ['m', 'rouge', 3]] } },
    ],
  },
  {
    owner: 'slim.benamor@webapp.local', slug: 'slim-electronique', name: 'Slim Électronique', category: 'electronique', city: 'ariana',
    cities: 'all', percent: 5, fee: 8, days: [2, 4], color: [51, 65, 85],
    description: 'Smartphones, audio et accessoires, garantie officielle. Livraison partout en Tunisie.',
    products: [
      { slug: 'samsung-galaxy-a55-5g', name: 'Samsung Galaxy A55 5G', category: 'smartphones', price: 1349, percent: 4, color: [30, 58, 138],
        description: 'Écran Super AMOLED 6,6", triple caméra 50 MP, batterie 5000 mAh.',
        values: { marque: 'Samsung', garantie: 12, ram: 8, couleur: 'bleu' }, variants: { axes: ['stockage'], rows: [['128-go', 6], ['256-go', 3, 1549]] } },
      { slug: 'xiaomi-redmi-note-13', name: 'Xiaomi Redmi Note 13', category: 'smartphones', price: 749, color: [17, 24, 39],
        description: 'Écran AMOLED 120 Hz, caméra 108 MP, charge rapide 33 W.',
        values: { marque: 'Xiaomi', garantie: 12, ram: 8 }, variants: { axes: ['stockage', 'couleur'], rows: [['128-go', 'noir', 10], ['256-go', 'noir', 4, 849], ['128-go', 'vert', 5], ['256-go', 'vert', 0, 849]] } },
      { slug: 'ecouteurs-jbl-tune-520bt', name: 'Casque JBL Tune 520BT', category: 'audio', price: 199, stock: 14, color: [37, 99, 235],
        description: 'Casque Bluetooth 5.3, son JBL Pure Bass, pliable.',
        values: { marque: 'JBL', garantie: 12, 'sans-fil': true, autonomie: 57 } },
      { slug: 'enceinte-bluetooth-portable', name: 'Enceinte Bluetooth portable étanche', category: 'audio', price: 129, stock: 20, color: [22, 163, 74],
        description: 'Étanche IPX7, 12 heures d’autonomie, idéale pour la plage.',
        values: { marque: 'Anker Soundcore', garantie: 18, 'sans-fil': true, autonomie: 12 } },
      { slug: 'chargeur-rapide-usb-c-33w', name: 'Chargeur rapide USB-C 33 W', category: 'accessoires-telephone', price: 45, stock: 60, color: [229, 231, 235],
        description: 'Charge rapide PD, câble USB-C fourni.',
        values: { marque: 'Baseus', garantie: 6, compatibilite: 'Android et iPhone 15' } },
      { slug: 'coque-silicone-galaxy-a55', name: 'Coque silicone Galaxy A55', category: 'accessoires-telephone', price: 25, stock: 35, color: [148, 163, 184],
        description: 'Silicone doux au toucher, protège caméra.',
        values: { marque: 'Samsung', compatibilite: 'Samsung Galaxy A55' } },
    ],
  },
  {
    owner: 'amine.jaziri@webapp.local', slug: 'amine-cosmetiques', name: 'Amine Cosmétiques', category: 'beaute', city: 'sfax',
    cities: ['sfax', 'sousse', 'gabes', 'mahdia', 'tunis'], percent: 18, fee: 7, days: [2, 3], color: [225, 29, 72],
    description: 'Cosmétiques naturels tunisiens : figue de Barbarie, argile tfal, savon noir et huiles pures.',
    products: [
      { slug: 'huile-figue-de-barbarie-30ml', name: 'Huile de figue de Barbarie bio — 30 ml', category: 'soins-visage', price: 89, compare: 110, stock: 22, percent: 20, color: [190, 18, 60],
        description: 'Première pression à froid, anti-âge naturel. Récoltée à Kasserine.',
        values: { contenance: 30, bio: true, 'type-de-peau': ['seche', 'normale', 'sensible'] } },
      { slug: 'creme-hydratante-argan', name: "Crème hydratante à l'huile d'argan — 50 ml", category: 'soins-visage', price: 39, stock: 30, color: [254, 240, 138],
        description: 'Hydrate et nourrit sans graisser.',
        values: { contenance: 50, bio: false, 'type-de-peau': ['mixte', 'normale'] } },
      { slug: 'savon-noir-eucalyptus', name: "Savon noir à l'eucalyptus", category: 'hammam-savons', price: 15, stock: 50, color: [63, 63, 70],
        description: 'Savon noir traditionnel du hammam, à l’huile d’olive et à l’eucalyptus. Pot de 250 g.',
        values: { bio: true, ingredients: "Huile d'olive, eucalyptus, potasse" } },
      { slug: 'argile-tfal-naturelle', name: 'Argile tfal naturelle', category: 'hammam-savons', price: 12, stock: 45, color: [214, 211, 209],
        description: 'Argile tunisienne pour le visage et les cheveux. Sachet de 200 g.',
        values: { bio: true, ingredients: 'Argile tfal 100 %' } },
      { slug: 'huile-ricin-cheveux-100ml', name: 'Huile de ricin pour cheveux — 100 ml', category: 'soins-cheveux', price: 19, stock: 40, color: [253, 186, 116],
        description: 'Fortifie et fait pousser les cheveux et les cils.',
        values: { contenance: 100, bio: true, 'type-de-cheveux': ['secs', 'boucles'] } },
      { slug: 'khol-traditionnel', name: 'Khôl traditionnel', category: 'maquillage', price: 14, stock: 60, color: [24, 24, 27],
        description: 'Khôl noir intense en pot, application au bâtonnet.',
        values: { teinte: 'Noir intense', bio: false } },
    ],
  },
  {
    owner: 'amel.zouari@webapp.local', slug: 'douceurs-d-amel', name: "Douceurs d'Amel", category: 'patisserie', city: 'nabeul',
    cities: ['nabeul', 'tunis', 'ariana', 'ben-arous', 'sousse'], percent: 12, fee: 6, days: [1, 2], color: [234, 88, 12],
    description: 'Pâtisserie tunisienne faite maison à Nabeul, au beurre et au miel. Commandes pour fêtes et mariages.',
    products: [
      { slug: 'baklawa-amandes-500g', name: 'Baklawa aux amandes — 500 g', category: 'patisserie-tunisienne', price: 38, stock: 20, color: [234, 179, 8],
        description: 'Feuilles de brick, amandes et pistaches, sirop au miel et à la fleur d’oranger.',
        values: { poids: 500, 'sans-gluten': false } },
      { slug: 'makroudh-kairouan-500g', name: 'Makroudh de Kairouan — 500 g', category: 'patisserie-tunisienne', price: 26, stock: 25, color: [146, 64, 14],
        description: 'Semoule et pâte de dattes, frit puis trempé dans le miel.',
        values: { poids: 500, 'sans-gluten': false } },
      { slug: 'kaak-warka-400g', name: 'Kaak warka — 400 g', category: 'patisserie-tunisienne', price: 42, stock: 12, color: [253, 230, 138],
        description: 'Anneaux à la pâte d’amande parfumée à l’eau de géranium.',
        values: { poids: 400, 'sans-gluten': false } },
      { slug: 'ghraiba-sorgho-500g', name: 'Ghraiba au sorgho — 500 g', category: 'patisserie-tunisienne', price: 24, stock: 18, color: [161, 98, 7],
        description: 'Fondante, au drôo (sorgho) et au beurre.',
        values: { poids: 500, 'sans-gluten': true } },
      { slug: 'bjawia-500g', name: 'Bjawia — 500 g', category: 'patisserie-tunisienne', price: 45, stock: 10, color: [254, 249, 195],
        description: 'Amandes, pistaches et noisettes, sucre glace et eau de rose.',
        values: { poids: 500, 'sans-gluten': true } },
      { slug: 'gateau-amandes-fleur-oranger', name: "Gâteau amandes & fleur d'oranger", category: 'gateaux', price: 65, stock: 5, color: [255, 237, 213],
        description: 'Moelleux, pour 8 personnes. À commander 24 h à l’avance.',
        values: { poids: 1000, 'sans-gluten': false, parts: 8 } },
    ],
  },
  {
    owner: 'mourad.ghanmi@webapp.local', slug: 'dar-zitouna', name: 'Dar Zitouna', category: 'epicerie-fine', city: 'sfax',
    cities: 'all', percent: 10, fee: 7, days: [2, 4], color: [77, 124, 15],
    description: 'Huile d’olive de nos oliviers de Sfax, dattes du Djérid et épices du Cap Bon.',
    products: [
      { slug: 'huile-olive-extra-vierge-1l', name: "Huile d'olive extra vierge — 1 L", category: 'huile-olive', price: 32, stock: 80, percent: 12, color: [132, 204, 22],
        description: 'Chemlali de Sfax, première pression à froid, récolte 2025.',
        values: { origine: 'Sfax', bio: false, contenance: 1000, qualite: 'extra-vierge' } },
      { slug: 'huile-olive-bio-500ml', name: "Huile d'olive bio — 500 ml", category: 'huile-olive', price: 22, stock: 50, color: [101, 163, 13],
        description: 'Certifiée bio, fruitée et légèrement ardente.',
        values: { origine: 'Sfax', bio: true, contenance: 500, qualite: 'extra-vierge' } },
      { slug: 'dattes-deglet-nour-1kg', name: 'Dattes Deglet Nour — 1 kg', category: 'dattes', price: 18, stock: 100, color: [180, 83, 9],
        description: 'Dattes branchées de Tozeur, moelleuses et translucides.',
        values: { origine: 'Tozeur', bio: false, poids: 1000, variete: 'deglet-nour' } },
      { slug: 'dattes-fourrees-amandes-500g', name: 'Dattes fourrées aux amandes — 500 g', category: 'dattes', price: 30, stock: 30, color: [120, 53, 15],
        description: 'Deglet Nour fourrées à la pâte d’amande, coffret cadeau.',
        values: { origine: 'Kébili', bio: false, poids: 500, variete: 'deglet-nour' } },
      { slug: 'harissa-cap-bon-250g', name: 'Harissa traditionnelle du Cap Bon — 250 g', category: 'epices-harissa', price: 9, stock: 70, color: [220, 38, 38],
        description: 'Piments séchés au soleil, ail et carvi, broyés à la main.',
        values: { origine: 'Nabeul', bio: false, poids: 250 } },
      { slug: 'tabel-epices-100g', name: 'Tabel (mélange d’épices) — 100 g', category: 'epices-harissa', price: 6, stock: 90, color: [202, 138, 4],
        description: 'Coriandre, carvi, ail et piment : le mélange des plats tunisiens.',
        values: { origine: 'Nabeul', bio: true, poids: 100 } },
    ],
  },
  {
    owner: 'riadh.mejri@webapp.local', slug: 'fakhar-nabeul', name: 'Fakhar Nabeul', category: 'maison-deco', city: 'nabeul',
    cities: ['nabeul', 'tunis', 'ariana', 'ben-arous', 'sousse', 'monastir'], percent: 10, fee: 12, days: [3, 6], color: [21, 128, 61],
    description: 'Poterie et céramique de Nabeul, kilims et margoums tissés à la main.',
    products: [
      { slug: 'plat-tajine-terre-cuite', name: 'Plat à tajine en terre cuite', category: 'cuisine', price: 35, stock: 15, color: [194, 65, 12],
        description: 'Terre cuite émaillée de Nabeul, va au four et sur le feu.',
        values: { dimensions: 'Ø 30 cm', 'fait-main': true, capacite: 2 } },
      { slug: 'couscoussier-aluminium', name: 'Couscoussier (keskes) 8 L', category: 'cuisine', price: 55, stock: 10, color: [148, 163, 184],
        description: 'Couscoussier deux étages en aluminium épais.',
        values: { dimensions: 'Ø 28 cm', 'fait-main': false, capacite: 8 } },
      { slug: 'assiettes-peintes-lot-6', name: 'Assiettes peintes à la main (lot de 6)', category: 'poterie-ceramique', price: 60, stock: 8, percent: 15, color: [37, 99, 235],
        description: 'Motifs traditionnels bleus et jaunes de Nabeul, peints à la main.',
        values: { dimensions: 'Ø 26 cm', 'fait-main': true } },
      { slug: 'vase-ceramique-bleu', name: 'Vase en céramique bleue', category: 'poterie-ceramique', price: 45, stock: 12, color: [30, 64, 175],
        description: 'Vase émaillé bleu de Nabeul, hauteur 35 cm.',
        values: { dimensions: 'H 35 cm', 'fait-main': true } },
      { slug: 'kilim-berbere-120x180', name: 'Kilim berbère 120 × 180 cm', category: 'tapis-kilims', price: 290, stock: 4, color: [185, 28, 28],
        description: 'Tissé à la main en laine, motifs géométriques du sud.',
        values: { dimensions: '120 × 180 cm', 'fait-main': true, matiere: 'laine' } },
      { slug: 'margoum-kairouan-160x230', name: 'Margoum de Kairouan 160 × 230 cm', category: 'tapis-kilims', price: 480, stock: 2, color: [124, 45, 18],
        description: 'Margoum tissé à Kairouan, laine teinte naturellement.',
        values: { dimensions: '160 × 230 cm', 'fait-main': true, matiere: 'laine' } },
    ],
  },
  {
    owner: 'sonia.benyoussef@webapp.local', slug: 'fadha-bijoux', name: 'Fadha Bijoux', category: 'bijoux', city: 'tunis',
    cities: 'all', percent: 15, fee: 7, days: [1, 3], color: [161, 98, 7],
    description: 'Bijoux en argent 925 et créations inspirées des bijoux berbères et de la khomsa.',
    products: [
      { slug: 'collier-khomsa-argent', name: 'Collier khomsa en argent 925', category: 'colliers', price: 85, stock: 15, percent: 20, color: [203, 213, 225],
        description: 'Main de Fatma ciselée, chaîne fine 45 cm.',
        values: { metal: 'argent-925', 'longueur-de-chaine': 45 } },
      { slug: 'collier-poisson-porte-bonheur', name: 'Collier poisson porte-bonheur', category: 'colliers', price: 55, stock: 20, color: [253, 224, 71],
        description: 'Pendentif poisson, symbole de chance, plaqué or.',
        values: { metal: 'plaque-or', 'longueur-de-chaine': 42 } },
      { slug: 'bague-berbere-argent', name: 'Bague berbère en argent', category: 'bagues', price: 65, color: [226, 232, 240],
        description: 'Bague large ciselée à la main, motifs berbères.',
        values: { metal: 'argent-925' }, variants: { axes: ['taille-de-bague'], rows: [['52', 3], ['54', 4], ['56', 2], ['58', 1]] } },
      { slug: 'boucles-khomsa-plaque-or', name: "Boucles d'oreilles khomsa plaqué or", category: 'boucles-oreilles', price: 49, stock: 25, color: [250, 204, 21],
        description: 'Petites khomsas pendantes, légères.',
        values: { metal: 'plaque-or' } },
      { slug: 'bracelet-mahdia-argent', name: 'Bracelet de Mahdia en argent', category: 'bracelets', price: 120, stock: 6, color: [241, 245, 249],
        description: 'Bracelet traditionnel de Mahdia, filigrane d’argent.',
        values: { metal: 'argent-925' } },
    ],
  },
  {
    owner: 'ahmed.kooli@webapp.local', slug: 'kooli-chaussures', name: 'Kooli Chaussures', category: 'chaussures', city: 'sfax',
    cities: 'all', percent: 10, fee: 8, days: [2, 4], color: [30, 41, 59],
    description: 'Chaussures en cuir fabriquées à Sfax, et balghas artisanales.',
    products: [
      { slug: 'baskets-cuir-blanches', name: 'Baskets en cuir blanches', category: 'baskets', price: 149, compare: 179, color: [248, 250, 252],
        description: 'Cuir pleine fleur, semelle cousue, fabriquées à Sfax.',
        values: { couleur: 'blanc' }, variants: { axes: ['pointure'], rows: [['40', 3], ['41', 4], ['42', 5], ['43', 3], ['44', 2], ['45', 0]] } },
      { slug: 'baskets-running-legeres', name: 'Baskets running légères', category: 'baskets', price: 119, color: [37, 99, 235],
        description: 'Mesh respirant, semelle amortissante.',
        values: {}, variants: { axes: ['pointure', 'couleur'], rows: [['40', 'noir', 4], ['42', 'noir', 6], ['44', 'noir', 3], ['40', 'bleu', 2], ['42', 'bleu', 3], ['44', 'bleu', 0]] } },
      { slug: 'sandales-cuir-femme', name: 'Sandales en cuir femme', category: 'sandales', price: 79, color: [180, 83, 9],
        description: 'Cuir naturel, bride ajustable.',
        values: { couleur: 'marron', matiere: 'cuir' }, variants: { axes: ['pointure'], rows: [['36', 2], ['37', 4], ['38', 5], ['39', 3], ['40', 1]] } },
      { slug: 'balgha-cuir-homme', name: 'Balgha en cuir homme', category: 'babouches-balghas', price: 59, color: [254, 243, 199],
        description: 'Balgha traditionnelle cousue main, cuir jaune.',
        values: { matiere: 'cuir' }, variants: { axes: ['pointure'], rows: [['41', 3], ['42', 4], ['43', 4], ['44', 2]] } },
      { slug: 'babouches-brodees-femme', name: 'Babouches brodées femme', category: 'babouches-balghas', price: 49, color: [190, 24, 93],
        description: 'Daim brodé de fils dorés, intérieur doux.',
        values: { matiere: 'daim', couleur: 'rose' }, variants: { axes: ['pointure'], rows: [['36', 3], ['37', 3], ['38', 4], ['39', 2]] } },
    ],
  },
  {
    owner: 'lamia.hadjali@webapp.local', slug: 'couffin-et-co', name: 'Couffin & Co', category: 'artisanat', city: 'sfax',
    cities: 'all', percent: 15, fee: 8, days: [3, 5], color: [161, 98, 7],
    description: 'Vannerie en palmier des îles Kerkennah et cuivre ciselé de la médina de Tunis.',
    products: [
      { slug: 'couffin-palmier-kerkennah', name: 'Couffin en palmier de Kerkennah', category: 'vannerie', price: 35, stock: 20, percent: 18, color: [217, 119, 6],
        description: 'Tressé à la main en feuilles de palmier, anses en cuir.',
        values: { region: 'Kerkennah', 'fait-main': true } },
      { slug: 'panier-rond-tresse', name: 'Panier rond tressé', category: 'vannerie', price: 25, stock: 15, color: [234, 179, 8],
        description: 'Panier de rangement en alfa, Ø 30 cm.',
        values: { region: 'Kasserine', 'fait-main': true } },
      { slug: 'chapeau-paille-mdhalla', name: 'Chapeau de paille (mdhalla)', category: 'vannerie', price: 20, stock: 30, color: [253, 224, 71],
        description: 'Le chapeau traditionnel des paysans, très large bord.',
        values: { region: 'Sfax', 'fait-main': true } },
      { slug: 'plateau-cuivre-cisele', name: 'Plateau en cuivre ciselé', category: 'cuivre', price: 120, stock: 5, color: [194, 65, 12],
        description: 'Plateau Ø 45 cm, ciselé à la main dans la médina de Tunis.',
        values: { region: 'Tunis', 'fait-main': true } },
      { slug: 'theiere-cuivre', name: 'Théière en cuivre', category: 'cuivre', price: 75, stock: 8, color: [234, 88, 12],
        description: 'Théière traditionnelle étamée à l’intérieur.',
        values: { region: 'Tunis', 'fait-main': true } },
    ],
  },
];
