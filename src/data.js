// ===== Данные захардкожены по мотивам видео-референса =====

const TEAMS = {
  "iron-wing":   { name: "Iron Wing",   color: "#8a8f98" },
  "team-spirit": { name: "Team Spirit", color: "#4fa8e8" },
  "team-vision": { name: "TEAM VISION", color: "#5fd0e8" },
  "boomboys":    { name: "BoomBoys",    color: "#e85f7a" },
  "team-liquid": { name: "Team Liquid", color: "#3a7bd5" },
  "team-yandex": { name: "Team Yandex", color: "#e8c34f" },
  "nigma-galaxy":{ name: "Nigma Galaxy",color: "#5a5fd5" },
  "team-falcons":{ name: "Team Falcons",color: "#2fbf8f" },
};

// Верхняя сетка: раунды -> матчи
const UPPER_BRACKET = [
  {
    round: "1/4",
    matches: [
      { id: "M1", date: "20 августа", format: "БО3", a: "iron-wing", b: "team-spirit", scoreA: 55, scoreB: 45 },
      { id: "M2", date: "20 августа", format: "БО3", a: "team-vision", b: "boomboys",   scoreA: 73, scoreB: 27 },
      { id: "M3", date: "20 августа", format: "БО2", a: "team-liquid", b: "team-yandex",scoreA: 55, scoreB: 45 },
      { id: "M4", date: "20 августа", format: "БО2", a: "nigma-galaxy", b: "team-falcons", scoreA: 46, scoreB: 54 },
    ]
  },
  {
    round: "1/2",
    matches: [
      { id: "M5", date: "21 августа", format: "БО3", a: "iron-wing", b: "team-vision", scoreA: 32, scoreB: 68 },
      { id: "M6", date: "21 августа", format: "БО3", a: "team-liquid", b: "team-falcons", scoreA: 53, scoreB: 47 },
    ]
  },
  {
    round: "Финал верхней сетки",
    matches: [
      { id: "M7", date: "22 августа", format: "БО3", a: "team-vision", b: "team-liquid", scoreA: 67, scoreB: 33 },
    ]
  },
];

const GRAND_FINAL = { id: "M14", date: "23 августа", format: "БО5", a: "team-vision", b: "team-liquid", scoreA: 71, scoreB: 29 };

// Нижняя сетка
const LOWER_BRACKET = [
  {
    round: "Раунд 1",
    matches: [
      { id: "M8", date: "21 августа", format: "БО3", a: "team-spirit", b: "boomboys", scoreA: 51, scoreB: 49 },
      { id: "M9", date: "21 августа", format: "БО3", a: "team-yandex", b: "nigma-galaxy", scoreA: 48, scoreB: 52 },
    ]
  },
  {
    round: "Раунд 2",
    matches: [
      { id: "M10", date: "22 августа", format: "БО3", a: "boomboys", b: "iron-wing", scoreA: 44, scoreB: 56 },
      { id: "M11", date: "22 августа", format: "БО3", a: "team-falcons", b: "boomboys", scoreA: 54, scoreB: 46 },
    ]
  },
  {
    round: "Раунд 3",
    matches: [
      { id: "M12", date: "22 августа", format: "БО3", a: "boomboys", b: "team-liquid", scoreA: 44, scoreB: 56 },
      { id: "M13", date: "22 августа", format: "БО3", a: "iron-wing", b: "team-yandex", scoreA: 54, scoreB: 46 },
    ]
  },
  {
    round: "Финал нижней сетки",
    matches: [
      { id: "M13b", date: "23 августа", format: "БО5", a: "team-liquid", b: "iron-wing", scoreA: 51, scoreB: 49 },
    ]
  },
];

// Лучшие связки по ролям (результат калькулятора фэнтези)
// pts — базовое значение при героя=160% / команды=140% без бонусов титула
const RESULT_ROLES = [
  {
    role: "Основа",
    subtitle: "Крипы · Командные сражения · Ку/М · Убийства терзателей · Смерти",
    weight: { hero: 0.6, team: 0.4 },
    rows: [
      { team: "team-vision", player: "Noticed 3 · Satanic 1", pts: 11013, note: "вместе 10 карт", tag: "" },
      { team: "team-spirit", player: "Collapse 3 · Yatoro 1", pts: 10688, note: "вместе 14 карт", tag: "8% 8.7" },
      { team: "team-liquid", player: "Ace 3 · m1CKe 1", pts: 10597, note: "вместе 14 карт", tag: "★ 14% 9.7" },
      { team: "team-falcons", player: "skiter 1 · ATF 3", pts: 10345, note: "вместе 17 карт", tag: "9% 9.2" },
      { team: "iron-wing", player: "33 3 · Puro 1", pts: 9840, note: "вместе 16 карт", tag: "★ 11% 9.3" },
      { team: "boomboys", player: "MiaRo 3 · Kiritych 1", pts: 9839, note: "вместе 16 карт", tag: "4% 8.0" },
      { team: "nigma-galaxy", player: "Sneyking 3 · 9Class 1", pts: 9302, note: "вместе 10 карт", tag: "3% 7.6" },
      { team: "team-yandex", player: "Malr1ne 3 · Larl 1", pts: 9110, note: "вместе 14 карт", tag: "2% 7.1" },
    ]
  },
  {
    role: "Центр",
    subtitle: "Убийства · Руны · Командные сражения · Смерти · Убийства терзателей",
    weight: { hero: 0.7, team: 0.3 },
    rows: [
      { team: "team-liquid", player: "Nisha 2", pts: 10049, note: "вместе 14 карт", tag: "★ 14% 9.7" },
      { team: "team-falcons", player: "MalrIne 2", pts: 9662, note: "вместе 17 карт", tag: "9% 9.2" },
      { team: "nigma-galaxy", player: "lorenof 2", pts: 9635, note: "вместе 10 карт", tag: "4% 8.0" },
      { team: "iron-wing", player: "larn 2", pts: 9533, note: "вместе 16 карт", tag: "★ 11% 9.3" },
      { team: "boomboys", player: "gpk~ 2", pts: 9307, note: "вместе 14 карт", tag: "4% 8.0" },
      { team: "team-vision", player: "Save~ 2", pts: 9245, note: "вместе 10 карт", tag: "★ 11% 11.0" },
      { team: "team-spirit", player: "Miposhka 2", pts: 9152, note: "вместе 14 карт", tag: "6% 8.4" },
      { team: "team-yandex", player: "Chris Luck 2", pts: 8930, note: "вместе 14 карт", tag: "2% 7.2" },
    ]
  },
  {
    role: "Поддержка",
    subtitle: "Установка вардов · Командные сражения · Убийства терзателей · Помощь",
    weight: { hero: 0.3, team: 0.7 },
    rows: [
      { team: "team-falcons", player: "Sneyking 1 · CCnC 4", pts: 9241, note: "вместе 17 карт", tag: "9% 9.2" },
      { team: "iron-wing", player: "Whitemon 5 · Ari~ 4", pts: 9152, note: "вместе 16 карт", tag: "★ 11% 9.3" },
      { team: "team-liquid", player: "rOtk 5 · Boni 4", pts: 8984, note: "вместе 14 карт", tag: "" },
      { team: "team-spirit", player: "cvM~me 4 · oub 5", pts: 8811, note: "вместе 14 карт", tag: "" },
      { team: "nigma-galaxy", player: "QH 5 · OmR 4", pts: 8640, note: "вместе 10 карт", tag: "" },
      { team: "team-vision", player: "Malady 4 · Zayac 5", pts: 8503, note: "вместе 10 карт", tag: "★ 11% 8.9" },
      { team: "boomboys", player: "Katomi 5 · Save 4", pts: 8354, note: "вместе 16 карт", tag: "" },
      { team: "team-yandex", player: "Sasha 5 · Nadín 4", pts: 8201, note: "вместе 14 карт", tag: "" },
    ]
  },
];

// Режимы вкладки «Прогнозы»: как считаются проценты/очки
const PREDICT_MODES = {
  tournament: { label: "Только Инт", scoreMult: 1,    chanceSkew: 1    },
  season:     { label: "По сезону",   scoreMult: 0.83, chanceSkew: 0.75 },
  freak:      { label: "Фрик",        scoreMult: 1.42, chanceSkew: 0.4  },
};

// Базовые значения score-card (режим "Только Инт")
const PREDICT_BASE = {
  score: 2338,
  avgCorrect: 5.2,
  usual: 1800,
  lucky: 5400,
  chances: [
    { n: 6, pct: 44, pts: 2520 },
    { n: 8, pct: 20, pts: 4320 },
    { n: 10, pct: 5.8, pts: 6600 },
    { n: 14, pct: 0.1, pts: 12000 },
  ],
};

// "Что пикают на Инте" — таблица префиксов
const PICK_TABLE = [
  { label: "Золотой", rate: "1,72 за карту", games: "409 банов", pct: "48%" },
  { label: "Лазурный", rate: "1,66 за карту", games: "199 банов", pct: "48%" },
  { label: "Элементальный", rate: "1,24 за карту", games: "104 бана", pct: "50%" },
  { label: "Королевский", rate: "0,98 за карту", games: "88 банов", pct: "44%" },
];

// Распределение "верных матчей" для гистограммы (0..14)
const DIST_CHART = [1, 3, 6, 11, 16, 20, 22, 20, 15, 10, 6, 3, 1, 0.5, 0.2];
