// PROTOTYPE: SquishySim web client
'use strict';

const DRIVES = ['hunger', 'thirst', 'fatigue', 'bladder', 'social', 'mood'];
const DEFAULT_DRIVES = { hunger: 0.10, thirst: 0.10, fatigue: 0.10, bladder: 0.00, social: 0.10, mood: 0.70 };

// ── SVG map constants ─────────────────────────────────────────────────────────
const SVG_NS = 'http://www.w3.org/2000/svg';

// Keyed by agent ID — add entries here if agents are added
const AGENT_COLORS = {
    alice:   '#4fc1ff',
    bob:     '#ce9178',
    charlie: '#c3e88d',
};

const RESOURCES = [
    { x: 5,  y: 5,  fill: '#81c784', label: 'F', name: 'Food' },
    { x: 15, y: 5,  fill: '#4fc3f7', label: 'W', name: 'Water' },
    { x: 15, y: 15, fill: '#bcaaa4', label: 'T', name: 'Latrine' },
    { x: 5,  y: 15, fill: '#ffb74d', label: 'S', name: 'Shelter' },
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

    // Resource icons: 1×1 square centered on position, with 1-char label + tooltip
    RESOURCES.forEach(r => {
        const rect = svgEl('rect', { x: r.x - 0.5, y: r.y - 0.5, width: 1, height: 1, fill: r.fill });
        rect.appendChild(svgTitle(r.name));
        g.appendChild(rect);
        const t = svgEl('text', {
            x: r.x, y: r.y,
            'font-size': '0.8', fill: '#1a1a1a',
            'text-anchor': 'middle', 'dominant-baseline': 'central',
            cursor: 'default',
        });
        t.textContent = r.label;
        g.appendChild(t);
    });
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
        const color = AGENT_COLORS[a.id] ?? '#888888';
        const isSelected = a.id === selectedAgentId;
        const r = isSelected ? '0.8' : '0.65';

        const circleAttrs = { cx: a.position.x, cy: a.position.y, r, fill: color };
        if (isSelected) { circleAttrs.stroke = '#ffffff'; circleAttrs['stroke-width'] = '0.15'; }
        const circle = svgEl('circle', circleAttrs);
        let tip = null;
        if (a.drives) {
            const d = a.drives;
            tip = `${a.name}\nhunger: ${d.hunger.toFixed(2)}  thirst: ${d.thirst.toFixed(2)}  fatigue: ${d.fatigue.toFixed(2)}\nbladder: ${d.bladder.toFixed(2)}  social: ${d.social.toFixed(2)}  mood: ${d.mood.toFixed(2)}\nnav: ${a.navState ?? ''}`;
            circle.appendChild(svgTitle(tip));
        }
        mapDynamic.appendChild(circle);

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
