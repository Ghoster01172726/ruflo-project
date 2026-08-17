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
  });
});

// ============================================================
// HELPERS
// ============================================================
function teamRow(teamKey, score, opponentScore, showPct){
  const team = TEAMS[teamKey] || { name: 'TBD', color: '#555' };
  const isWinner = typeof score === 'number' && typeof opponentScore === 'number' && score > opponentScore;
  const pct = showPct ? Math.round((score / (score + opponentScore)) * 100) : null;
  return `
    <div class="team-row ${isWinner ? 'is-winner' : ''}">
      <span class="team-dot" style="background:${team.color}"></span>
      <span class="team-row__name">${team.name}</span>
      ${pct !== null ? `<span class="team-row__pct">${pct}%</span>` : ''}
      <span class="team-row__score">${score ?? '–'}</span>
    </div>`;
}

function matchCard(match, showPct){
  const isTbd = !match.a || !match.b;
  return `
    <div class="match ${isTbd ? 'match--tbd' : ''} ${match.id.startsWith('M14') ? 'match--final' : ''} ${showPct ? 'match--predict' : ''}">
      <div class="match__meta">
        <span>${match.id} · ${match.date}</span>
        <span class="match__format">${match.format}</span>
      </div>
      ${teamRow(match.a, match.scoreA, match.scoreB, showPct)}
      ${teamRow(match.b, match.scoreB, match.scoreA, showPct)}
    </div>`;
}

function renderBracket(container, rounds, showPct){
  container.innerHTML = rounds.map(round => `
    <div class="bracket-round">
      <p class="bracket-round__label">${round.round}</p>
      <div class="bracket-round__matches">
        ${round.matches.map(m => matchCard(m, showPct)).join('')}
      </div>
    </div>
  `).join('');
}

// ============================================================
// RENDER: BRACKET TAB
// ============================================================
renderBracket(document.getElementById('upper-bracket'), UPPER_BRACKET, false);
renderBracket(document.getElementById('lower-bracket'), LOWER_BRACKET, false);

// ============================================================
// RENDER: PREDICT TAB (upper bracket + grand final, with %)
// ============================================================
const predictRounds = [...UPPER_BRACKET, { round: 'Гранд-финал', matches: [GRAND_FINAL] }];
renderBracket(document.getElementById('predict-bracket'), predictRounds, true);

// ============================================================
// RENDER: DISTRIBUTION CHART
// ============================================================
const distChart = document.getElementById('dist-chart');
const maxVal = Math.max(...DIST_CHART);
distChart.innerHTML = DIST_CHART.map(v => {
  const h = Math.max(4, Math.round((v / maxVal) * 100));
  const isPeak = v === maxVal;
  return `<i style="height:${h}%" class="${isPeak ? 'is-peak' : ''}"></i>`;
}).join('');

// ============================================================
// RENDER: PICK TABLE (Что пикают на Инте)
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
// RENDER: RESULT GRID (Лучшие связки)
// ============================================================
const resultGrid = document.getElementById('result-grid');
resultGrid.innerHTML = RESULT_ROLES.map(col => `
  <div class="result-col">
    <div class="result-col__head">
      <p class="result-col__role">${col.role}</p>
      <p class="result-col__sub">${col.subtitle}</p>
    </div>
    ${col.rows.map(row => {
      const team = TEAMS[row.team] || { name: 'TBD', color: '#555' };
      return `
        <div class="lineup-row ${row.top ? 'is-top' : ''}">
          <span class="lineup-row__rank">${row.rank}</span>
          <span class="lineup-row__mid">
            <span class="lineup-row__team">
              <span class="lineup-row__team-dot" style="background:${team.color}"></span>
              ${team.name}
            </span>
            <span class="lineup-row__players">${row.player}</span>
          </span>
          <span class="lineup-row__right">
            <span class="lineup-row__pts">${row.pts}</span>
            <span class="lineup-row__note">${row.note}</span>
            ${row.tag ? `<span class="lineup-row__tag">${row.tag}</span>` : ''}
          </span>
        </div>`;
    }).join('')}
  </div>
`).join('');

// ============================================================
// FILTERBAR toggle (visual only)
// ============================================================
document.querySelectorAll('.filterbar').forEach(bar => {
  bar.addEventListener('click', (e) => {
    const btn = e.target.closest('.filterbar__btn');
    if (!btn) return;
    bar.querySelectorAll('.filterbar__btn').forEach(b => b.classList.remove('is-active'));
    btn.classList.add('is-active');
  });
});

// ============================================================
// BRACKET ACTIONS (demo interactivity)
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
  document.querySelectorAll('.team-row__score').forEach(el => { el.textContent = '–'; });
  document.querySelectorAll('.team-row').forEach(el => el.classList.remove('is-winner'));
});

if (btnShare) btnShare.addEventListener('click', () => {
  btnShare.textContent = 'Скопировано в буфер ✓';
  setTimeout(() => { btnShare.textContent = 'Поделиться прогнозом'; }, 1800);
});
