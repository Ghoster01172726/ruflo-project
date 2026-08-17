// ============================================================
// HELPERS
// ============================================================
function initials(name){
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[1][0]).toUpperCase();
}

function formatNum(n){
  return Math.round(n).toLocaleString('ru-RU').replace(/ /g, ' ');
}

// ============================================================
// TABS
// ============================================================
const tabBtns = document.querySelectorAll('.tabs__btn');
const panels = document.querySelectorAll('.panel');

tabBtns.forEach(btn => {
  btn.addEventListener('click', () => {
    const target = btn.dataset.tab;
    tabBtns.forEach(b => {
      b.classList.toggle('is-active', b === btn);
      b.setAttribute('aria-selected', b === btn ? 'true' : 'false');
    });
    panels.forEach(p => {
      p.hidden = p.dataset.panel !== target;
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });

    requestAnimationFrame(() => {
      if (target === 'bracket') {
        drawConnectors(document.getElementById('upper-bracket'));
        drawConnectors(document.getElementById('lower-bracket'));
      } else if (target === 'predict') {
        drawConnectors(document.getElementById('predict-bracket'));
      }
    });
  });
});

// ============================================================
// BRACKET CONNECTORS (SVG lines between rounds)
// ============================================================
function drawConnectors(container){
  if (!container) return;
  const old = container.querySelector('svg.bracket-connectors');
  if (old) old.remove();

  const rounds = Array.from(container.querySelectorAll('.bracket-round'));
  if (rounds.length < 2) return;

  const containerRect = container.getBoundingClientRect();
  const width = container.scrollWidth;
  const height = container.scrollHeight;
  if (!width || !height) return;

  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('class', 'bracket-connectors');
  svg.setAttribute('width', width);
  svg.setAttribute('height', height);
  svg.style.overflow = 'visible';

  for (let r = 0; r < rounds.length - 1; r++){
    const matches = Array.from(rounds[r].querySelectorAll('.match'));
    const nextMatches = Array.from(rounds[r + 1].querySelectorAll('.match'));
    if (!matches.length || !nextMatches.length) continue;

    matches.forEach(m => {
      const mRect = m.getBoundingClientRect();
      const startX = mRect.right - containerRect.left + container.scrollLeft;
      const startY = mRect.top + mRect.height / 2 - containerRect.top + container.scrollTop;

      let best = nextMatches[0];
      let bestDist = Infinity;
      nextMatches.forEach(nm => {
        const nRect = nm.getBoundingClientRect();
        const nY = nRect.top + nRect.height / 2 - containerRect.top + container.scrollTop;
        const d = Math.abs(nY - startY);
        if (d < bestDist) { bestDist = d; best = nm; }
      });

      const nRect = best.getBoundingClientRect();
      const endX = nRect.left - containerRect.left + container.scrollLeft;
      const endY = nRect.top + nRect.height / 2 - containerRect.top + container.scrollTop;
      const midX = startX + (endX - startX) / 2;

      const path = document.createElementNS(ns, 'path');
      path.setAttribute('d', `M ${startX} ${startY} H ${midX} V ${endY} H ${endX}`);
      path.setAttribute('fill', 'none');
      path.setAttribute('stroke', '#3a2c18');
      path.setAttribute('stroke-width', '1.5');
      svg.appendChild(path);
    });
  }

  container.prepend(svg);
}

let resizeTimer;
window.addEventListener('resize', () => {
  clearTimeout(resizeTimer);
  resizeTimer = setTimeout(() => {
    document.querySelectorAll('.panel:not([hidden]) .bracket').forEach(b => drawConnectors(b));
  }, 150);
});

// ============================================================
// BRACKET TAB (upper / lower — actual results)
// ============================================================
function teamRow(teamKey, score, opponentScore){
  const team = TEAMS[teamKey] || { name: 'TBD', color: '#555' };
  const isWinner = typeof score === 'number' && typeof opponentScore === 'number' && score > opponentScore;
  return `
    <div class="team-row ${isWinner ? 'is-winner' : ''}">
      <span class="team-badge" style="background:${team.color}">${initials(team.name)}</span>
      <span class="team-row__name">${team.name}</span>
      <span class="team-row__score">${score ?? '–'}</span>
    </div>`;
}

function matchCard(match){
  const isTbd = !match.a || !match.b;
  return `
    <div class="match ${isTbd ? 'match--tbd' : ''} ${match.id.startsWith('M14') ? 'match--final' : ''}">
      <div class="match__meta">
        <span>${match.id} · ${match.date}</span>
        <span class="match__format">${match.format}</span>
      </div>
      ${teamRow(match.a, match.scoreA, match.scoreB)}
      ${teamRow(match.b, match.scoreB, match.scoreA)}
    </div>`;
}

function renderBracket(container, rounds){
  container.innerHTML = rounds.map(round => `
    <div class="bracket-round">
      <p class="bracket-round__label">${round.round}</p>
      <div class="bracket-round__matches">
        ${round.matches.map(m => matchCard(m)).join('')}
      </div>
    </div>
  `).join('');
  drawConnectors(container);
}

renderBracket(document.getElementById('upper-bracket'), UPPER_BRACKET);
renderBracket(document.getElementById('lower-bracket'), LOWER_BRACKET);

// ============================================================
// PREDICT TAB (win-chance % per mode + score card)
// ============================================================
function skewPct(pct, skew){
  return Math.round(50 + (pct - 50) * skew);
}

function predictTeamRow(teamKey, pct, opponentPct){
  const team = TEAMS[teamKey] || { name: 'TBD', color: '#555' };
  const isWinner = pct > opponentPct;
  return `
    <div class="team-row ${isWinner ? 'is-winner' : ''}">
      <span class="team-badge" style="background:${team.color}">${initials(team.name)}</span>
      <span class="team-row__name">${team.name}</span>
      <span class="team-row__pct">${pct}%</span>
      <span class="team-row__score">${pct}</span>
    </div>`;
}

function predictMatchCard(match, cfg){
  const isTbd = !match.a || !match.b;
  const pctA = skewPct(match.scoreA, cfg.chanceSkew);
  const pctB = 100 - pctA;
  return `
    <div class="match match--predict ${isTbd ? 'match--tbd' : ''} ${match.id.startsWith('M14') ? 'match--final' : ''}">
      <div class="match__meta">
        <span>${match.id} · ${match.date}</span>
        <span class="match__format">${match.format}</span>
      </div>
      ${predictTeamRow(match.a, pctA, pctB)}
      ${predictTeamRow(match.b, pctB, pctA)}
    </div>`;
}

function renderPredictBracket(mode){
  const cfg = PREDICT_MODES[mode];
  const rounds = [...UPPER_BRACKET, { round: 'Гранд-финал', matches: [GRAND_FINAL] }];
  const container = document.getElementById('predict-bracket');
  container.innerHTML = rounds.map(round => `
    <div class="bracket-round">
      <p class="bracket-round__label">${round.round}</p>
      <div class="bracket-round__matches">
        ${round.matches.map(m => predictMatchCard(m, cfg)).join('')}
      </div>
    </div>
  `).join('');
  drawConnectors(container);
}

function renderScoreCard(mode){
  const cfg = PREDICT_MODES[mode];
  const score = Math.round(PREDICT_BASE.score * cfg.scoreMult);
  const avg = (PREDICT_BASE.avgCorrect * cfg.scoreMult).toFixed(1).replace('.', ',');
  const usual = Math.round(PREDICT_BASE.usual * cfg.scoreMult);
  const lucky = Math.round(PREDICT_BASE.lucky * cfg.scoreMult);
  const delta = score - PREDICT_BASE.score;

  document.getElementById('score-value').textContent = formatNum(score);
  document.getElementById('score-avg').textContent = avg;
  document.getElementById('score-correct').textContent = avg;
  document.getElementById('score-usual').textContent = formatNum(usual);
  document.getElementById('score-lucky').textContent = formatNum(lucky);

  const deltaEl = document.getElementById('score-delta');
  deltaEl.textContent = (delta >= 0 ? '+' : '−') + formatNum(Math.abs(delta));
  deltaEl.className = 'delta ' + (delta >= 0 ? 'delta--up' : 'delta--down');

  const chancesContainer = document.getElementById('score-chances');
  chancesContainer.innerHTML = '<p class="score-card__chances-title">Шанс набрать</p>' +
    PREDICT_BASE.chances.map(c => {
      const pts = Math.round(c.pts * cfg.scoreMult);
      const pctVal = +(c.pct * cfg.chanceSkew).toFixed(1);
      const barWidth = Math.min(100, Math.max(2, pctVal * 2));
      const label = c.pct < 1 ? `<span class="chance-row__pct chance-row__pct--min">меньше ${pctVal}%</span>`
        : `<span class="chance-row__pct">${pctVal.toString().replace('.', ',')}%</span>`;
      return `
        <div class="chance-row">
          <span class="chance-row__n">${c.n}</span>
          ${label}
          <span class="chance-row__bar"><i style="width:${barWidth}%"></i></span>
          <span class="chance-row__pts">${formatNum(pts)}</span>
        </div>`;
    }).join('');

  const distChart = document.getElementById('dist-chart');
  const scaled = DIST_CHART.map(v => v * (0.85 + 0.15 * cfg.scoreMult));
  const maxVal = Math.max(...scaled);
  distChart.innerHTML = scaled.map(v => {
    const h = Math.max(4, Math.round((v / maxVal) * 100));
    return `<i style="height:${h}%" class="${v === maxVal ? 'is-peak' : ''}"></i>`;
  }).join('');
}

function applyPredictMode(mode){
  renderPredictBracket(mode);
  renderScoreCard(mode);
}

applyPredictMode('tournament');

const predictFilter = document.querySelector('[data-role="predict-filter"]');
if (predictFilter) {
  predictFilter.addEventListener('click', (e) => {
    const btn = e.target.closest('.filterbar__btn');
    if (!btn) return;
    predictFilter.querySelectorAll('.filterbar__btn').forEach(b => b.classList.remove('is-active'));
    btn.classList.add('is-active');
    applyPredictMode(btn.dataset.mode);
  });
}

// ============================================================
// PICK TABLE (Что пикают на Инте)
// ============================================================
const pickRows = document.getElementById('pick-table-rows');
const maxRate = Math.max(...PICK_TABLE.map(r => parseFloat(r.rate)));
pickRows.innerHTML = PICK_TABLE.map(row => {
  const rateNum = parseFloat(row.rate);
  const width = Math.round((rateNum / maxRate) * 100);
  return `
    <div class="pick-row">
      <span class="pick-row__label">${row.label}</span>
      <span class="pick-row__bar"><i style="width:${width}%"></i></span>
      <span class="pick-row__rate">${row.rate}</span>
      <span class="pick-row__pct">${row.pct}</span>
    </div>`;
}).join('');

// ============================================================
// FANTASY CALCULATOR (live: props + coach title -> lineups)
// ============================================================
const coachBonus = { prefix: 0, suffix: 0 };

function heroInputs(){ return Array.from(document.querySelectorAll('.prop-input[data-group="hero"]')); }
function teamInputs(){ return Array.from(document.querySelectorAll('.prop-input[data-group="team"]')); }

function avgOf(inputs, fallback){
  const vals = inputs.map(i => parseFloat(i.value)).filter(v => !isNaN(v) && v > 0);
  if (!vals.length) return fallback;
  return vals.reduce((a, b) => a + b, 0) / vals.length;
}

function computeRoleRows(role){
  const heroAvg = avgOf(heroInputs(), 160);
  const teamAvg = avgOf(teamInputs(), 140);
  const w = role.weight;
  const mult = Math.max(0.3, 1 + w.hero * ((heroAvg - 160) / 160) + w.team * ((teamAvg - 140) / 140));
  const totalBonus = coachBonus.prefix + coachBonus.suffix;

  const rows = role.rows.map(r => ({ ...r, computedPts: r.pts * mult }));
  rows.sort((a, b) => b.computedPts - a.computedPts);
  rows.forEach((r, i) => {
    const bonusShare = i === 0 ? 1 : i === 1 ? 0.5 : i === 2 ? 0.25 : 0;
    r.computedPts = Math.round(r.computedPts + totalBonus * bonusShare);
    r.rank = i + 1;
    r.top = i === 0;
  });
  return rows;
}

function renderResultGrid(){
  const resultGrid = document.getElementById('result-grid');
  const allPts = [];

  resultGrid.innerHTML = RESULT_ROLES.map(role => {
    const rows = computeRoleRows(role);
    allPts.push(...rows.map(r => r.computedPts));
    return `
      <div class="result-col">
        <div class="result-col__head">
          <p class="result-col__role">${role.role}</p>
          <p class="result-col__sub">${role.subtitle}</p>
        </div>
        ${rows.map(row => {
          const team = TEAMS[row.team] || { name: 'TBD', color: '#555' };
          return `
            <div class="lineup-row ${row.top ? 'is-top' : ''}">
              <span class="lineup-row__rank">${row.rank}</span>
              <span class="lineup-row__mid">
                <span class="lineup-row__team">
                  <span class="lineup-row__team-badge" style="background:${team.color}">${initials(team.name)}</span>
                  ${team.name}
                </span>
                <span class="lineup-row__players">${row.player}</span>
              </span>
              <span class="lineup-row__right">
                <span class="lineup-row__pts">${formatNum(row.computedPts)}</span>
                <span class="lineup-row__note">${row.note}</span>
                ${row.tag ? `<span class="lineup-row__tag">${row.tag}</span>` : ''}
              </span>
            </div>`;
        }).join('')}
      </div>
    `;
  }).join('');

  const avgScore = allPts.length ? allPts.reduce((a, b) => a + b, 0) / allPts.length : 0;
  document.getElementById('avg-score-stat').textContent = formatNum(avgScore);
}

renderResultGrid();

document.querySelectorAll('.prop-input').forEach(inp => {
  inp.addEventListener('input', renderResultGrid);
});

document.querySelectorAll('[data-role="coach-chips"]').forEach(group => {
  group.addEventListener('click', (e) => {
    const btn = e.target.closest('.chip--pick');
    if (!btn) return;
    const wasActive = btn.classList.contains('is-active');
    group.querySelectorAll('.chip--pick').forEach(b => b.classList.remove('is-active'));
    const bonusKey = group.dataset.group;
    if (wasActive) {
      coachBonus[bonusKey] = 0;
    } else {
      btn.classList.add('is-active');
      coachBonus[bonusKey] = parseInt(btn.dataset.bonus, 10) || 0;
    }
    renderResultGrid();
  });
});

const btnHint = document.getElementById('btn-hint');
if (btnHint) btnHint.addEventListener('click', () => {
  document.querySelectorAll('.lineup-row.is-top').forEach(row => {
    row.style.transition = 'transform .2s ease';
    row.style.transform = 'scale(1.02)';
    setTimeout(() => { row.style.transform = ''; }, 260);
  });
});

const btnRecalc = document.getElementById('btn-recalc');
if (btnRecalc) btnRecalc.addEventListener('click', () => {
  renderResultGrid();
  const original = btnRecalc.textContent;
  btnRecalc.textContent = 'Пересчитано ✓';
  setTimeout(() => { btnRecalc.textContent = original; }, 1400);
});

// ============================================================
// FILTERBAR toggle (visual only — bracket tab rating filter)
// ============================================================
document.querySelectorAll('.filterbar:not([data-role="predict-filter"])').forEach(bar => {
  bar.addEventListener('click', (e) => {
    const btn = e.target.closest('.filterbar__btn');
    if (!btn) return;
    bar.querySelectorAll('.filterbar__btn').forEach(b => b.classList.remove('is-active'));
    btn.classList.add('is-active');
  });
});

// ============================================================
// BRACKET ACTIONS (Bracket tab: fill / clear / share)
// ============================================================
const btnFill = document.getElementById('btn-fill');
const btnClear = document.getElementById('btn-clear');
const btnShare = document.getElementById('btn-share');

if (btnFill) btnFill.addEventListener('click', () => {
  document.querySelectorAll('#upper-bracket .team-row, #lower-bracket .team-row').forEach(row => {
    row.style.transition = 'background .3s ease';
    row.style.background = 'rgba(232,184,75,0.08)';
    setTimeout(() => { row.style.background = ''; }, 500);
  });
});

if (btnClear) btnClear.addEventListener('click', () => {
  document.querySelectorAll('#upper-bracket .team-row__score, #lower-bracket .team-row__score').forEach(el => { el.textContent = '–'; });
  document.querySelectorAll('#upper-bracket .team-row, #lower-bracket .team-row').forEach(el => el.classList.remove('is-winner'));
});

if (btnShare) btnShare.addEventListener('click', () => {
  btnShare.textContent = 'Скопировано в буфер ✓';
  setTimeout(() => { btnShare.textContent = 'Поделиться прогнозом'; }, 1800);
});
