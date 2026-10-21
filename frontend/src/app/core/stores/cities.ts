/** The 24 Tunisian governorates — same codes as the backend (Governorates). Labels: `cities.<code>`. */
export const CITIES = [
  'tunis', 'ariana', 'ben-arous', 'manouba', 'nabeul', 'zaghouan', 'bizerte', 'beja',
  'jendouba', 'kef', 'siliana', 'sousse', 'monastir', 'mahdia', 'sfax', 'kairouan',
  'kasserine', 'sidi-bouzid', 'gabes', 'medenine', 'tataouine', 'gafsa', 'tozeur', 'kebili',
] as const;

export type City = (typeof CITIES)[number];
