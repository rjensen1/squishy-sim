// PROTOTYPE: SquishySim web client
'use strict';

const DRIVES = ['hunger', 'thirst', 'fatigue', 'bladder', 'social', 'mood', 'suppressionBudget'];
const DEFAULT_DRIVES = { hunger: 0.10, thirst: 0.10, fatigue: 0.10, bladder: 0.00, social: 0.10, mood: 0.70, suppressionBudget: 1.0 };

// ── SVG map constants ─────────────────────────────────────────────────────────
const SVG_NS = 'http://www.w3.org/2000/svg';

// Keyed by agent ID — add entries here if agents are added
const AGENT_COLORS = {
    alice:   '#4fc1ff',
    bob:     '#ce9178',
    charlie: '#c3e88d',
};

const RESOURCES = [
    { x: 5,  y: 5,  name: 'Food',    elements: [
        { tag: 'circle',  attrs: { cx: 5,     cy: 5,     r: 0.38, fill: 'none', stroke: '#81c784', 'stroke-width': 0.10 } },
        { tag: 'circle',  attrs: { cx: 5,     cy: 5,     r: 0.15, fill: '#81c784' } },
    ]},
    { x: 15, y: 5,  name: 'Water',   elements: [
        { tag: 'path',    attrs: { d: 'M15,4.55 L15.35,5.05 Q15.35,5.42 15,5.42 Q14.65,5.42 14.65,5.05 Z', fill: '#4fc3f7' } },
    ]},
    { x: 15, y: 15, name: 'Latrine', elements: [
        { tag: 'rect',    attrs: { x: 14.88, y: 14.62, width: 0.24, height: 0.76, rx: 0.04, fill: '#bcaaa4' } },
        { tag: 'rect',    attrs: { x: 14.62, y: 14.88, width: 0.76, height: 0.24, rx: 0.04, fill: '#bcaaa4' } },
    ]},
    { x: 5,  y: 15, name: 'Shelter', elements: [
        { tag: 'polygon', attrs: { points: '5,14.55 5.42,15.42 4.58,15.42', fill: '#ffb74d' } },
    ]},
];

let selectedAgentId = null;
let showingConversations = false;

// ── Bootstrap ────────────────────────────────────────────────────────────────

window.addEventListener('DOMContentLoaded', () => {
    bindControls();
    initMapStatic();
    refresh();
    setInterval(refresh, 1500);
});

function bindControls() {
    document.getElementById('btn-step').addEventListener('click', () => api('POST', '/sim/step').then(refresh));
    document.getElementById('btn-pause').addEventListener('click', () => api('POST', '/sim/pause').then(refresh));
    document.getElementById('btn-resume').addEventListener('click', () => api('POST', '/sim/resume').then(refresh));
    document.getElementById('speed-select').addEventListener('change', e =>
        api('POST', '/sim/speed', { multiplier: parseFloat(e.target.value) }));

    document.getElementById('btn-conversations').addEventListener('click', () => {
        showingConversations = !showingConversations;
        document.getElementById('btn-conversations').classList.toggle('active', showingConversations);
        document.getElementById('agent-detail').classList.toggle('hidden', showingConversations);
        document.getElementById('conversations-panel').classList.toggle('hidden', !showingConversations);
        if (showingConversations) refreshConversations();
    });

    document.getElementById('btn-reset-drives').addEventListener('click', resetDrives);
    document.getElementById('btn-save-llm').addEventListener('click', saveLlm);
}

// ── Main refresh cycle ───────────────────────────────────────────────────────

async function refresh() {
    const [agentsData, statusData] = await Promise.all([
        api('GET', '/agents'),
        api('GET', '/sim/status'),
    ]);

    document.getElementById('tick-display').textContent = `Tick ${statusData.tick}`;
    document.getElementById('version-display').textContent = statusData.version ?? '';

    const isPaused = statusData.paused;
    document.getElementById('btn-pause').classList.toggle('active', isPaused === true);
    document.getElementById('btn-resume').classList.toggle('active', isPaused === false);

    renderAgentList(agentsData);
    renderSimMap(agentsData);
    if (selectedAgentId) refreshAgentDetail();
    if (showingConversations) refreshConversations();
}

// ── Agent list ───────────────────────────────────────────────────────────────

function renderAgentList(agents) {
    const ul = document.getElementById('agent-list');
    // Preserve selection; only rebuild if agent set changed
    const ids = agents.map(a => a.id).join(',');
    if (ul.dataset.ids === ids) return;
    ul.dataset.ids = ids;
    ul.innerHTML = '';
    agents.forEach(a => {
        const li = document.createElement('li');
        li.textContent = a.name;
        li.dataset.id = a.id;
        if (a.id === selectedAgentId) li.classList.add('selected');
        li.addEventListener('click', () => selectAgent(a.id));
        ul.appendChild(li);
    });
    if (!selectedAgentId && agents.length > 0) selectAgent(agents[0].id);
}

function selectAgent(id) {
    selectedAgentId = id;
    showingConversations = false;
    document.getElementById('btn-conversations').classList.remove('active');
    document.getElementById('agent-detail').classList.remove('hidden');
    document.getElementById('conversations-panel').classList.add('hidden');
    document.querySelectorAll('#agent-list li').forEach(li =>
        li.classList.toggle('selected', li.dataset.id === id));
    refreshAgentDetail();
}

// ── Agent detail ─────────────────────────────────────────────────────────────

async function refreshAgentDetail() {
    if (!selectedAgentId) return;
    const agent = await api('GET', `/agents/${selectedAgentId}`);
    document.getElementById('agent-name').textContent = agent.name;
    renderDrives(agent.drives);
    document.getElementById('current-action').textContent = agent.currentAction;
    document.getElementById('current-reason').textContent = agent.currentReason;
    document.getElementById('spatial-status').textContent = agent.navState ?? '';
    const snapEl = document.getElementById('snap-status');
    if (snapEl) { snapEl.textContent = agent.isSnapped ? '⚠ SNAPPED' : ''; }
    document.getElementById('llm-model').value = agent.llmConfig.model;
    document.getElementById('llm-url').value   = agent.llmConfig.baseUrl;
    await refreshThoughts();
    await refreshAgentMessages();
}

function renderDrives(drives) {
    const grid = document.getElementById('drives-grid');
    DRIVES.forEach(d => {
        let row = document.getElementById(`drive-row-${d}`);
        if (!row) {
            row = document.createElement('div');
            row.className = 'drive-row';
            row.id = `drive-row-${d}`;
            row.innerHTML = `
                <span class="drive-label">${d}</span>
                <div class="drive-bar-bg"><div class="drive-bar-fill" id="bar-${d}"></div></div>
                <input class="drive-slider" id="slider-${d}" type="range" min="0" max="100" step="1">
                <span class="drive-num" id="num-${d}"></span>`;
            grid.appendChild(row);

            document.getElementById(`slider-${d}`).addEventListener('input', e => {
                const val = parseInt(e.target.value) / 100;
                updateDriveUI(d, val);
            });
            document.getElementById(`slider-${d}`).addEventListener('change', e => {
                const val = parseInt(e.target.value) / 100;
                api('POST', `/agents/${selectedAgentId}/drives/${d}`, { value: val });
            });
        }

        const val = drives[d];
        updateDriveUI(d, val);
    });
}

function updateDriveUI(drive, val) {
    const bar = document.getElementById(`bar-${drive}`);
    const slider = document.getElementById(`slider-${drive}`);
    const num = document.getElementById(`num-${drive}`);
    if (!bar) return;
    const pct = Math.round(val * 100);
    bar.style.width = `${pct}%`;
    if (drive === 'mood') {
        bar.className = 'drive-bar-fill' + (val < 0.15 ? ' crit' : val < 0.35 ? ' warn' : '');
    } else if (drive === 'suppressionBudget') {
        // Inverted: tiers map to spec thresholds (high > 0.50, medium 0.25–0.50, low/snap <= 0.25)
        bar.className = 'drive-bar-fill' + (val <= 0.25 ? ' crit' : val <= 0.50 ? ' warn' : '');
    } else {
        bar.className = 'drive-bar-fill' + (val > 0.85 ? ' crit' : val > 0.65 ? ' warn' : '');
    }
    slider.value = pct;
    num.textContent = val.toFixed(2);
}

async function refreshThoughts() {
    const thoughts = await api('GET', `/agents/${selectedAgentId}/thoughts?limit=20`);
    const log = document.getElementById('thought-log');
    log.innerHTML = '';
    thoughts.forEach(t => {
        const div = document.createElement('div');
        div.className = 'log-entry';
        div.innerHTML = `<span class="ts">${fmtTime(t.timestamp)}</span><span class="text">${esc(t.text)}</span>`;
        log.appendChild(div);
    });
    log.scrollTop = log.scrollHeight;
}

async function refreshAgentMessages() {
    const msgs = await api('GET', `/agents/${selectedAgentId}/messages`);
    const el = document.getElementById('agent-messages');
    el.innerHTML = '';
    msgs.forEach(m => {
        const div = document.createElement('div');
        div.className = 'log-entry';
        div.innerHTML = `<span class="ts">${fmtTime(m.timestamp)}</span><span class="from">${esc(m.fromAgentId)}</span><span class="to">→ ${esc(m.toAgentId)}</span><span class="text">${esc(m.text)}</span>`;
        el.appendChild(div);
    });
    el.scrollTop = el.scrollHeight;
}

async function refreshConversations() {
    const msgs = await api('GET', '/conversations');
    const feed = document.getElementById('global-feed');
    feed.innerHTML = '';
    msgs.forEach(m => {
        const div = document.createElement('div');
        div.className = 'log-entry';
        div.innerHTML = `<span class="ts">${fmtTime(m.timestamp)}</span><span class="from">${esc(m.fromAgentId)}</span><span class="to">→ ${esc(m.toAgentId)}</span><span class="text">${esc(m.text)}</span>`;
        feed.appendChild(div);
    });
    feed.scrollTop = feed.scrollHeight;
}

// ── Actions ──────────────────────────────────────────────────────────────────

async function resetDrives() {
    if (!selectedAgentId) return;
    for (const [d, v] of Object.entries(DEFAULT_DRIVES)) {
        await api('POST', `/agents/${selectedAgentId}/drives/${d}`, { value: v });
    }
    refreshAgentDetail();
}

async function saveLlm() {
    if (!selectedAgentId) return;
    const model   = document.getElementById('llm-model').value.trim();
    const baseUrl = document.getElementById('llm-url').value.trim();
    const apiKey  = document.getElementById('llm-key').value || null;
    await api('PUT', `/agents/${selectedAgentId}/llm`, { model, baseUrl, apiKey });
    document.getElementById('llm-key').value = '';
}

// ── SVG Map ───────────────────────────────────────────────────────────────────

function svgEl(tag, attrs) {
    const el = document.createElementNS(SVG_NS, tag);
    for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
    return el;
}

function svgTitle(text) {
    const t = document.createElementNS(SVG_NS, 'title');
    t.textContent = text;
    return t;
}

// Render static layer once on load: grid lines + resource icons
function initMapStatic() {
    const g = document.getElementById('map-static');
    if (!g) return;

    // Faint grid lines at 5-unit intervals (4 lines each axis)
    for (let i = 5; i < 20; i += 5) {
        g.appendChild(svgEl('line', { x1: i, y1: 0, x2: i, y2: 20, stroke: '#2a2a2a', 'stroke-width': '0.1' }));
        g.appendChild(svgEl('line', { x1: 0, y1: i, x2: 20, y2: i, stroke: '#2a2a2a', 'stroke-width': '0.1' }));
    }

    // Resource icons: distinct SVG shapes per resource type, with tooltip on first element
    RESOURCES.forEach(r => {
        r.elements.forEach((e, i) => {
            const el = svgEl(e.tag, e.attrs);
            if (i === 0) el.appendChild(svgTitle(r.name));
            g.appendChild(el);
        });
    });
}

// Blend a hex color toward #888888 (gray) by factor [0,1].
// factor=0 → original color; factor=1 → full gray.
function blendTowardGray(hex, factor) {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    const gray = 136;  // 0x88
    const br = Math.round(r + (gray - r) * factor);
    const bg = Math.round(g + (gray - g) * factor);
    const bb = Math.round(b + (gray - b) * factor);
    return `#${br.toString(16).padStart(2, '0')}${bg.toString(16).padStart(2, '0')}${bb.toString(16).padStart(2, '0')}`;
}

// Redrawn each poll: nav state lines (first) then agent circles (on top)
function renderSimMap(agents) {
    const mapDynamic = document.getElementById('map-dynamic');
    if (!mapDynamic) return;
    mapDynamic.innerHTML = '';

    // Draw nav lines first so circles render on top
    agents.forEach(a => {
        if (!a.position || !a.destination) return;
        const state = (a.navState ?? '').toLowerCase();
        let stroke, strokeWidth, dashArray;

        if (state === 'committed') {
            stroke = 'rgba(255,255,255,0.35)'; strokeWidth = '0.2'; dashArray = '2,1';
        } else if (state === 'seeking') {
            stroke = 'rgba(129,199,132,0.6)'; strokeWidth = '0.2'; dashArray = '2,1';
        } else if (state === 'preempted') {
            stroke = 'rgba(255,138,101,0.85)'; strokeWidth = '0.25'; dashArray = null;
        } else {
            return; // Idle — no line
        }

        const attrs = {
            x1: a.position.x, y1: a.position.y,
            x2: a.destination.x, y2: a.destination.y,
            stroke, 'stroke-width': strokeWidth,
        };
        if (dashArray) attrs['stroke-dasharray'] = dashArray;
        mapDynamic.appendChild(svgEl('line', attrs));
    });

    // Draw agent circles on top of lines
    agents.forEach(a => {
        if (!a.position) return;
        const baseColor = AGENT_COLORS[a.id] ?? '#888888';
        // Desaturate toward gray proportional to isolation (behavioral coherence degradation).
        // Visually distinct from snap (red ring): isolation is chronic/gradual, snap is acute.
        const isolationFactor = a.behavioralCoherence != null ? Math.max(0, 1.0 - a.behavioralCoherence) : 0;
        const color = isolationFactor > 0 ? blendTowardGray(baseColor, isolationFactor) : baseColor;
        const isSelected = a.id === selectedAgentId;
        const r = isSelected ? '0.8' : '0.65';

        const circleAttrs = { cx: a.position.x, cy: a.position.y, r, fill: color };
        if (isSelected) { circleAttrs.stroke = '#ffffff'; circleAttrs['stroke-width'] = '0.15'; }
        const circle = svgEl('circle', circleAttrs);
        let tip = null;
        if (a.drives) {
            const d = a.drives;
            const budgetLabel = d.suppressionBudget <= 0.25 ? 'LOW' : d.suppressionBudget <= 0.50 ? 'MED' : 'OK';
            const driftLabel = a.personaDriftFactor != null ? a.personaDriftFactor.toFixed(2) : '?';
            const coherenceLabel = a.behavioralCoherence != null ? a.behavioralCoherence.toFixed(2) : '?';
            const snappedTag = a.isSnapped ? ' [SNAPPED]' : '';
            const driftTag = a.personaDriftFactor > 0.4 ? ' [DRIFTED]' : '';
            tip = `${a.name}${snappedTag}${driftTag}\nhunger: ${d.hunger.toFixed(2)}  thirst: ${d.thirst.toFixed(2)}  fatigue: ${d.fatigue.toFixed(2)}\nbladder: ${d.bladder.toFixed(2)}  social: ${d.social.toFixed(2)}  mood: ${d.mood.toFixed(2)}\nnav: ${a.navState ?? ''}  budget: ${budgetLabel}\ncoherence: ${coherenceLabel}  drift: ${driftLabel}`;
            circle.appendChild(svgTitle(tip));
        }
        mapDynamic.appendChild(circle);

        // Snap indicator: red outer ring when agent is snapped
        if (a.isSnapped) {
            const snapRing = svgEl('circle', {
                cx: a.position.x, cy: a.position.y,
                r: parseFloat(r) + 0.25,
                fill: 'none', stroke: '#f44747', 'stroke-width': '0.2',
            });
            mapDynamic.appendChild(snapRing);
        }

        // Initial of agent name as label — also carries tooltip so hovering the letter works
        const label = a.name ? a.name[0] : a.id[0].toUpperCase();
        const t = svgEl('text', {
            x: a.position.x, y: a.position.y,
            'font-size': '0.7', fill: '#1a1a1a',
            'text-anchor': 'middle', 'dominant-baseline': 'central',
            cursor: 'default',
        });
        t.textContent = label;
        if (tip) t.appendChild(svgTitle(tip));
        mapDynamic.appendChild(t);
    });
}

// ── Helpers ───────────────────────────────────────────────────────────────────

async function api(method, path, body) {
    const opts = { method, headers: { 'Content-Type': 'application/json' } };
    if (body) opts.body = JSON.stringify(body);
    const res = await fetch(path, opts);
    if (!res.ok) { console.error(`${method} ${path} → ${res.status}`); return {}; }
    return res.json();
}

function esc(s) {
    return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

function fmtTime(ts) {
    return new Date(ts).toLocaleTimeString('en-US', { hour12: false,
        hour: '2-digit', minute: '2-digit', second: '2-digit' });
}
