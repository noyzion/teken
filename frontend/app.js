const API_BASE = '/api';

let positions = [];
let soldiers = [];
let schedule = [];
let settings = { shiftHours: 8 };
let editMode = false;

const DAYS_OF_WEEK = ['ראשון', 'שני', 'שלישי', 'רביעי', 'חמישי', 'שישי', 'שבת'];
let selectedDayForHours = null; // יום נבחר לבחירת שעות

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    setupTabs();
    loadData();
    setDefaultDates();
    setupDaysGroup();
    setupDayHoursSelector();
    
    // Setup constraint soldier select change event
    const constraintSoldierSelect = document.getElementById('constraintSoldier');
    if (constraintSoldierSelect) {
        constraintSoldierSelect.addEventListener('change', function() {
            loadSoldierConstraints(this.value);
        });
    }

    // Setup Enter key for settings
    document.getElementById('globalShiftHours').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') saveSettings();
    });
});

function setupTabs() {
    const tabButtons = document.querySelectorAll('.tab-btn');
    const tabContents = document.querySelectorAll('.tab-content');

    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetTab = btn.getAttribute('data-tab');
            
            tabButtons.forEach(b => b.classList.remove('active'));
            tabContents.forEach(c => c.classList.remove('active'));
            
            btn.classList.add('active');
            document.getElementById(targetTab).classList.add('active');
            
            if (targetTab === 'constraints') {
                updateConstraintsUI();
            } else if (targetTab === 'view') {
                loadSchedule();
            } else if (targetTab === 'settings') {
                loadSettings();
            } else if (targetTab === 'saved') {
                loadSavedSchedules();
            }
        });
    });
}

function setupDaysGroup() {
    const daysGroup = document.getElementById('forbiddenDaysGroup');
    if (!daysGroup) return;
    
    daysGroup.innerHTML = '';
    DAYS_OF_WEEK.forEach((dayName, index) => {
        const label = document.createElement('label');
        label.innerHTML = `
            <input type="checkbox" value="${index}" class="day-checkbox">
            <span>${dayName}</span>
        `;
        daysGroup.appendChild(label);
    });
}

function setupDayHoursSelector() {
    const daySelectorTabs = document.getElementById('daySelectorTabs');
    const hoursGroup = document.getElementById('hoursForDayGroup');
    
    if (!daySelectorTabs || !hoursGroup) return;
    
    // Create day selector tabs
    daySelectorTabs.innerHTML = '';
    DAYS_OF_WEEK.forEach((dayName, index) => {
        const tab = document.createElement('button');
        tab.type = 'button';
        tab.className = 'day-selector-tab';
        tab.textContent = dayName;
        tab.dataset.day = index;
        tab.addEventListener('click', () => selectDayForHours(index));
        daySelectorTabs.appendChild(tab);
    });
    
    // Setup hours group (will be updated when day is selected)
    updateHoursForSelectedDay();
}

function selectDayForHours(dayIndex) {
    selectedDayForHours = dayIndex;
    
    // Update active tab
    document.querySelectorAll('.day-selector-tab').forEach(tab => {
        tab.classList.toggle('active', parseInt(tab.dataset.day) === dayIndex);
    });
    
    // Update hours display
    updateHoursForSelectedDay();
}

function updateHoursForSelectedDay() {
    const hoursGroup = document.getElementById('hoursForDayGroup');
    if (!hoursGroup) return;
    
    if (selectedDayForHours === null) {
        hoursGroup.innerHTML = '<p style="text-align: center; color: #9ca3af; padding: 20px;">בחר יום מהרשימה למעלה כדי להגדיר שעות אסורות</p>';
        return;
    }
    
    hoursGroup.innerHTML = '';
    for (let hour = 0; hour < 24; hour++) {
        const label = document.createElement('label');
        label.innerHTML = `
            <input type="checkbox" value="${hour}" class="hour-checkbox-day" data-day="${selectedDayForHours}">
            <span>${hour.toString().padStart(2, '0')}:00</span>
        `;
        hoursGroup.appendChild(label);
    }
}

function setDefaultDates() {
    const today = new Date();
    const nextWeek = new Date(today);
    nextWeek.setDate(nextWeek.getDate() + 7);
    
    document.getElementById('startDate').value = today.toISOString().split('T')[0];
    document.getElementById('endDate').value = nextWeek.toISOString().split('T')[0];
}

async function loadData() {
    await loadSettings();
    await loadPositions();
    await loadSoldiers();
}

// Settings Management
async function loadSettings() {
    try {
        const response = await fetch(`${API_BASE}/settings`);
        settings = await response.json();
        document.getElementById('globalShiftHours').value = settings.shiftHours || 8;
    } catch (error) {
        console.error('Error loading settings:', error);
    }
}

async function saveSettings() {
    const shiftHours = parseInt(document.getElementById('globalShiftHours').value);

    if (!shiftHours || shiftHours < 1) {
        showNotification('אנא הזן מספר שעות תקין', 'error');
        return;
    }

    const statusDiv = document.getElementById('settingsStatus');
    statusDiv.innerHTML = '<p>שומר...</p>';
    statusDiv.className = '';

    try {
        const response = await fetch(`${API_BASE}/settings`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ shiftHours: shiftHours })
        });

        if (response.ok) {
            settings = await response.json();
            statusDiv.innerHTML = '<p class="success">הגדרות נשמרו בהצלחה!</p>';
            statusDiv.className = 'success';
        } else {
            const error = await response.text();
            statusDiv.innerHTML = `<p class="error">${error}</p>`;
            statusDiv.className = 'error';
        }
    } catch (error) {
        console.error('Error saving settings:', error);
        statusDiv.innerHTML = '<p class="error">שגיאה בשמירת ההגדרות</p>';
        statusDiv.className = 'error';
    }
}

async function loadPositions() {
    try {
        const response = await fetch(`${API_BASE}/positions`);
        positions = await response.json();
        displayPositions();
    } catch (error) {
        console.error('Error loading positions:', error);
    }
}

async function loadSoldiers() {
    try {
        const response = await fetch(`${API_BASE}/soldiers`);
        soldiers = await response.json();
        displaySoldiers();
    } catch (error) {
        console.error('Error loading soldiers:', error);
    }
}


// Positions Management
async function addPosition() {
    const name = document.getElementById('positionName').value.trim();
    const requiresCommander = document.getElementById('positionRequiresCommander').checked;
    const isStandby = document.getElementById('positionIsStandby').checked;

    if (!name) {
        showNotification('אנא הזן שם עמדה', 'error');
        return;
    }

    const newPosition = { 
        name: name,
        requiresCommander: requiresCommander,
        isStandby: isStandby
    };

    try {
        const response = await fetch(`${API_BASE}/positions`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(newPosition)
        });

        if (response.ok) {
            const created = await response.json();
            positions.push(created);
            document.getElementById('positionName').value = '';
            document.getElementById('positionRequiresCommander').checked = false;
            document.getElementById('positionIsStandby').checked = false;
            document.getElementById('positionName').focus();
            displayPositions();
            updateConstraintsUI();
            showNotification(`עמדה "${name}" נוספה בהצלחה`, 'success');
        } else {
            const error = await response.text();
            showNotification(`שגיאה: ${error}`, 'error');
        }
    } catch (error) {
        console.error('Error adding position:', error);
        showNotification('שגיאה בהוספת העמדה', 'error');
    }
}

function displayPositions() {
    const list = document.getElementById('positionsList');
    if (positions.length === 0) {
        list.innerHTML = `
            <div class="empty-state">
                <p>אין עמדות מוגדרות</p>
                <p style="font-size: 0.9em; margin-top: 10px; color: #9ca3af;">הוסף עמדות כדי להתחיל</p>
            </div>
        `;
        return;
    }

    list.innerHTML = `
        <div style="margin-bottom: 15px; display: flex; gap: 10px; align-items: center;">
            <button class="btn-secondary" id="selectAllPositionsBtn" onclick="selectAllPositions()">
                <span>בחר הכל</span>
            </button>
            <button class="btn-secondary" id="deselectAllPositionsBtn" onclick="deselectAllPositions()" style="display: none;">
                <span>בטל בחירה</span>
            </button>
            <button class="btn-delete" id="deleteSelectedPositionsBtn" onclick="deleteSelectedPositions()" style="display: none;">
                <span>מחק נבחרים</span>
            </button>
        </div>
        ${positions.map(pos => `
        <div class="list-item">
            <div class="info" style="display: flex; align-items: center; gap: 12px;">
                <label class="checkbox-label" style="margin: 0;">
                    <input type="checkbox" class="position-checkbox" value="${pos.id}" onchange="updatePositionSelection()">
                </label>
                <div>
                    <strong>${pos.name}</strong>
                    ${pos.requiresCommander ? '<span style="margin-right: 8px; padding: 4px 8px; background: #fef3c7; color: #92400e; border-radius: 6px; font-size: 0.85em; font-weight: 600;">דורש מפקד</span>' : ''}
                    ${pos.isStandby ? '<span style="margin-right: 8px; padding: 4px 8px; background: #dbeafe; color: #1e40af; border-radius: 6px; font-size: 0.85em; font-weight: 600;">כוננות</span>' : '<span style="margin-right: 8px; padding: 4px 8px; background: #fce7f3; color: #831843; border-radius: 6px; font-size: 0.85em; font-weight: 600;">שמירה</span>'}
                </div>
            </div>
            <div class="actions">
                <button class="btn-delete" onclick="deletePosition('${pos.id}')">מחק</button>
            </div>
        </div>
    `).join('')}`;
}

function selectAllPositions() {
    document.querySelectorAll('.position-checkbox').forEach(cb => cb.checked = true);
    updatePositionSelection();
}

function deselectAllPositions() {
    document.querySelectorAll('.position-checkbox').forEach(cb => cb.checked = false);
    updatePositionSelection();
}

function updatePositionSelection() {
    const selected = document.querySelectorAll('.position-checkbox:checked').length;
    const selectAllBtn = document.getElementById('selectAllPositionsBtn');
    const deselectAllBtn = document.getElementById('deselectAllPositionsBtn');
    const deleteBtn = document.getElementById('deleteSelectedPositionsBtn');
    
    if (selected > 0) {
        selectAllBtn.style.display = 'none';
        deselectAllBtn.style.display = 'inline-block';
        deleteBtn.style.display = 'inline-block';
        deleteBtn.innerHTML = `<span>מחק נבחרים (${selected})</span>`;
    } else {
        selectAllBtn.style.display = 'inline-block';
        deselectAllBtn.style.display = 'none';
        deleteBtn.style.display = 'none';
    }
}

async function deleteSelectedPositions() {
    const selected = Array.from(document.querySelectorAll('.position-checkbox:checked'))
        .map(cb => cb.value);
    
    if (selected.length === 0) {
        showNotification('אנא בחר עמדות למחיקה', 'error');
        return;
    }
    
    const selectedNames = selected.map(id => positions.find(p => p.id === id)?.name).filter(Boolean);
    if (!confirm(`האם אתה בטוח שברצונך למחוק ${selected.length} עמדות?\n${selectedNames.join(', ')}`)) {
        return;
    }
    
    let successCount = 0;
    let failCount = 0;
    
    for (const id of selected) {
        try {
            const response = await fetch(`${API_BASE}/positions/${id}`, {
                method: 'DELETE'
            });
            
            if (response.ok) {
                successCount++;
            } else {
                failCount++;
            }
        } catch (error) {
            console.error('Error deleting position:', error);
            failCount++;
        }
    }
    
    // Reload positions
    await loadPositions();
    updateConstraintsUI();
    
    if (failCount === 0) {
        showNotification(`נמחקו ${successCount} עמדות בהצלחה`, 'success');
    } else {
        showNotification(`נמחקו ${successCount} עמדות, ${failCount} נכשלו`, 'warning');
    }
}

async function deletePosition(id) {
    if (!confirm('האם אתה בטוח שברצונך למחוק את העמדה?')) return;

    try {
        const response = await fetch(`${API_BASE}/positions/${id}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            const position = positions.find(p => p.id === id);
            positions = positions.filter(p => p.id !== id);
            displayPositions();
            updateConstraintsUI();
            showNotification(`עמדה "${position.name}" נמחקה`, 'success');
        } else {
            showNotification('שגיאה במחיקת העמדה', 'error');
        }
    } catch (error) {
        console.error('Error deleting position:', error);
        showNotification('שגיאה במחיקת העמדה', 'error');
    }
}

// Soldiers Management
function switchSoldierInput(mode) {
    const singleInput = document.getElementById('soldierName');
    const multipleInput = document.getElementById('soldiersListInput');
    const addSingleBtn = document.querySelector('#soldiers button.btn-primary');
    const addMultipleBtn = document.getElementById('addMultipleBtn');
    const tabs = document.querySelectorAll('.input-tab');
    const singleCommanderGroup = document.getElementById('singleSoldierCommanderGroup');
    const multipleCommanderGroup = document.getElementById('multipleSoldiersCommanderGroup');

    tabs.forEach(tab => tab.classList.remove('active'));
    
    if (mode === 'single') {
        singleInput.style.display = 'block';
        multipleInput.style.display = 'none';
        addSingleBtn.style.display = 'inline-block';
        addMultipleBtn.style.display = 'none';
        singleCommanderGroup.style.display = 'block';
        multipleCommanderGroup.style.display = 'none';
        tabs[0].classList.add('active');
        singleInput.focus();
    } else {
        singleInput.style.display = 'none';
        multipleInput.style.display = 'block';
        addSingleBtn.style.display = 'none';
        addMultipleBtn.style.display = 'inline-block';
        singleCommanderGroup.style.display = 'none';
        multipleCommanderGroup.style.display = 'block';
        tabs[1].classList.add('active');
        multipleInput.focus();
    }
}

async function addSoldier() {
    const name = document.getElementById('soldierName').value.trim();
    const isCommander = document.getElementById('soldierIsCommander').checked;

    if (!name) {
        showNotification('אנא הזן שם חייל', 'error');
        return;
    }

    await addSoldierByName(name, isCommander);
    document.getElementById('soldierName').value = '';
    document.getElementById('soldierIsCommander').checked = false;
    document.getElementById('soldierName').focus();
}

async function addMultipleSoldiers() {
    const input = document.getElementById('soldiersListInput').value.trim();
    const markAllAsCommanders = document.getElementById('markAllAsCommanders').checked;
    
    if (!input) {
        showNotification('אנא הזן רשימת שמות', 'error');
        return;
    }

    // Parse names - support multiple formats
    let names = [];
    
    // Try splitting by newlines first
    if (input.includes('\n')) {
        names = input.split('\n').map(n => n.trim()).filter(n => n);
    }
    // Then try comma
    else if (input.includes(',')) {
        names = input.split(',').map(n => n.trim()).filter(n => n);
    }
    // Then try space (but only if multiple words)
    else {
        const words = input.split(/\s+/).filter(w => w);
        // If it's just one word, treat as single name
        if (words.length === 1) {
            names = [words[0]];
        } else {
            // Try to group words into names (heuristic: 2 words per name)
            names = [];
            for (let i = 0; i < words.length; i += 2) {
                if (i + 1 < words.length) {
                    names.push(words[i] + ' ' + words[i + 1]);
                } else {
                    names.push(words[i]);
                }
            }
        }
    }

    if (names.length === 0) {
        showNotification('לא נמצאו שמות תקינים', 'error');
        return;
    }

    // Add all soldiers
    let successCount = 0;
    let failCount = 0;

    for (const name of names) {
        if (name) {
            try {
                await addSoldierByName(name, markAllAsCommanders);
                successCount++;
            } catch (error) {
                failCount++;
            }
        }
    }

    // Clear input
    document.getElementById('soldiersListInput').value = '';
    document.getElementById('markAllAsCommanders').checked = false;
    
    // Show result
    if (failCount === 0) {
        showNotification(`נוספו ${successCount} חיילים בהצלחה!`, 'success');
    } else {
        showNotification(`נוספו ${successCount} חיילים, ${failCount} נכשלו`, 'warning');
    }
    
    displaySoldiers();
    updateConstraintsUI();
}

async function addSoldierByName(name, isCommander = false) {
    const newSoldier = {
        name: name,
        isCommander: isCommander,
        constraints: {}
    };

    const response = await fetch(`${API_BASE}/soldiers`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newSoldier)
    });

    if (response.ok) {
        const created = await response.json();
        soldiers.push(created);
        return created;
    } else {
        const error = await response.text();
        throw new Error(error);
    }
}

function displaySoldiers() {
    const list = document.getElementById('soldiersList');
    if (soldiers.length === 0) {
        list.innerHTML = `
            <div class="empty-state">
                <p>אין חיילים מוגדרים</p>
                <p style="font-size: 0.9em; margin-top: 10px; color: #9ca3af;">הוסף חיילים כדי להתחיל</p>
            </div>
        `;
        return;
    }

    const commandersCount = soldiers.filter(s => s.isCommander).length;

    list.innerHTML = `
        <div style="margin-bottom: 15px; padding: 12px; background: #f0f9ff; border-radius: 8px; color: #0369a1; font-weight: 500;">
            סה"כ ${soldiers.length} חיילים ${commandersCount > 0 ? `(${commandersCount} מפקדים)` : ''}
        </div>
        ${soldiers.map(soldier => `
        <div class="list-item">
            <div class="info">
                <strong>${soldier.name}</strong>
                ${soldier.isCommander ? '<span style="margin-right: 8px; padding: 4px 8px; background: #dbeafe; color: #1e40af; border-radius: 6px; font-size: 0.85em; font-weight: 600;">מפקד</span>' : ''}
            </div>
            <div class="actions">
                <label class="checkbox-label" style="margin-left: 10px; margin-right: 10px;">
                    <input type="checkbox" ${soldier.isCommander ? 'checked' : ''} 
                           onchange="toggleCommander('${soldier.id}', this.checked)" 
                           class="modern-checkbox">
                    <span style="font-size: 0.9em;">מפקד</span>
                </label>
                <button class="btn-delete" onclick="deleteSoldier('${soldier.id}')">מחק</button>
            </div>
        </div>
    `).join('')}`;
}

async function toggleCommander(id, isCommander) {
    const soldier = soldiers.find(s => s.id === id);
    if (!soldier) return;

    soldier.isCommander = isCommander;

    try {
        const response = await fetch(`${API_BASE}/soldiers/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(soldier)
        });

        if (response.ok) {
            const updated = await response.json();
            const index = soldiers.findIndex(s => s.id === id);
            if (index !== -1) {
                soldiers[index] = updated;
            }
            displaySoldiers();
            showNotification(isCommander ? `"${soldier.name}" סומן כמפקד` : `"${soldier.name}" בוטל סימון כמפקד`, 'success');
        } else {
            // Revert on error
            soldier.isCommander = !isCommander;
            displaySoldiers();
            showNotification('שגיאה בעדכון סטטוס המפקד', 'error');
        }
    } catch (error) {
        console.error('Error toggling commander:', error);
        // Revert on error
        soldier.isCommander = !isCommander;
        displaySoldiers();
        showNotification('שגיאה בעדכון סטטוס המפקד', 'error');
    }
}

async function deleteSoldier(id) {
    const soldier = soldiers.find(s => s.id === id);
    if (!confirm(`האם אתה בטוח שברצונך למחוק את ${soldier.name}?`)) return;

    try {
        const response = await fetch(`${API_BASE}/soldiers/${id}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            soldiers = soldiers.filter(s => s.id !== id);
            displaySoldiers();
            updateConstraintsUI();
            showNotification(`חייל "${soldier.name}" נמחק`, 'success');
        } else {
            showNotification('שגיאה במחיקת החייל', 'error');
        }
    } catch (error) {
        console.error('Error deleting soldier:', error);
        showNotification('שגיאה במחיקת החייל', 'error');
    }
}

// Constraints Management
function updateConstraintsUI() {
    const soldierSelect = document.getElementById('constraintSoldier');
    soldierSelect.innerHTML = '<option value="">בחר חייל</option>' +
        soldiers.map(s => `<option value="${s.id}">${s.name}</option>`).join('');

    const forbiddenPositionsDiv = document.getElementById('forbiddenPositions');
    
    forbiddenPositionsDiv.innerHTML = positions.map(pos => `
        <label>
            <input type="checkbox" value="${pos.id}" class="forbidden-pos">
            <span>${pos.name}</span>
        </label>
    `).join('');

    displayConstraints();
}

function loadSoldierConstraints(soldierId) {
    // Reset all
    document.querySelectorAll('.hour-checkbox-day').forEach(cb => cb.checked = false);
    document.querySelectorAll('.day-checkbox').forEach(cb => cb.checked = false);
    document.querySelectorAll('.forbidden-pos').forEach(cb => cb.checked = false);
    
    // Reset selected day
    selectedDayForHours = null;
    document.querySelectorAll('.day-selector-tab').forEach(tab => tab.classList.remove('active'));
    updateHoursForSelectedDay();

    if (!soldierId) {
        return;
    }

    const soldier = soldiers.find(s => s.id === soldierId);
    if (!soldier || !soldier.constraints) {
        return;
    }

    // Load forbidden days (full days)
    if (soldier.constraints.forbiddenDaysOfWeek && soldier.constraints.forbiddenDaysOfWeek.length > 0) {
        soldier.constraints.forbiddenDaysOfWeek.forEach(day => {
            const checkbox = document.querySelector(`.day-checkbox[value="${day}"]`);
            if (checkbox) checkbox.checked = true;
        });
    }

    // Load forbidden hours by day
    if (soldier.constraints.forbiddenHoursByDay) {
        Object.keys(soldier.constraints.forbiddenHoursByDay).forEach(dayStr => {
            const day = parseInt(dayStr);
            const hours = soldier.constraints.forbiddenHoursByDay[dayStr];
            if (hours && hours.length > 0) {
                hours.forEach(hour => {
                    const checkbox = document.querySelector(`.hour-checkbox-day[data-day="${day}"][value="${hour}"]`);
                    if (checkbox) checkbox.checked = true;
                });
            }
        });
    }

    // Load forbidden positions
    if (soldier.constraints.forbiddenPositions && soldier.constraints.forbiddenPositions.length > 0) {
        soldier.constraints.forbiddenPositions.forEach(posId => {
            const checkbox = document.querySelector(`.forbidden-pos[value="${posId}"]`);
            if (checkbox) checkbox.checked = true;
        });
    }
}

function displayConstraints() {
    const list = document.getElementById('constraintsList');
    const soldiersWithConstraints = soldiers.filter(s => 
        s.constraints && Object.keys(s.constraints).length > 0 &&
        ((s.constraints.forbiddenDaysOfWeek && s.constraints.forbiddenDaysOfWeek.length > 0) || 
         (s.constraints.forbiddenHoursByDay && Object.keys(s.constraints.forbiddenHoursByDay).length > 0) ||
         (s.constraints.forbiddenPositions && s.constraints.forbiddenPositions.length > 0))
    );

    if (soldiersWithConstraints.length === 0) {
        list.innerHTML = `
            <div class="empty-state">
                <p>אין אילוצים מוגדרים</p>
                <p style="font-size: 0.9em; margin-top: 10px; color: #9ca3af;">הגדר אילוצים כדי לשלוט בשיבוץ</p>
            </div>
        `;
        return;
    }

    list.innerHTML = soldiersWithConstraints.map(soldier => {
        const constraints = [];
        
        // Forbidden days (full days)
        if (soldier.constraints.forbiddenDaysOfWeek && soldier.constraints.forbiddenDaysOfWeek.length > 0) {
            const dayNames = soldier.constraints.forbiddenDaysOfWeek
                .map(day => DAYS_OF_WEEK[day])
                .filter(Boolean);
            constraints.push(`ימים שלמים אסורים: ${dayNames.join(', ')}`);
        }
        
        // Forbidden hours by day
        if (soldier.constraints.forbiddenHoursByDay) {
            const hoursByDay = [];
            Object.keys(soldier.constraints.forbiddenHoursByDay).forEach(dayStr => {
                const day = parseInt(dayStr);
                const hours = soldier.constraints.forbiddenHoursByDay[dayStr];
                if (hours && hours.length > 0) {
                    const sortedHours = [...hours].sort((a, b) => a - b);
                    hoursByDay.push(`${DAYS_OF_WEEK[day]}: ${sortedHours.map(h => h.toString().padStart(2, '0') + ':00').join(', ')}`);
                }
            });
            if (hoursByDay.length > 0) {
                constraints.push(`שעות אסורות לפי יום: ${hoursByDay.join('; ')}`);
            }
        }
        
        // Forbidden positions
        if (soldier.constraints.forbiddenPositions && soldier.constraints.forbiddenPositions.length > 0) {
            const forbiddenNames = soldier.constraints.forbiddenPositions
                .map(id => positions.find(p => p.id === id)?.name)
                .filter(Boolean);
            constraints.push(`עמדות אסורות: ${forbiddenNames.join(', ')}`);
        }

        return `
            <div class="list-item">
                <div class="info">
                    <strong>${soldier.name}</strong>
                    <div style="margin-top: 8px; color: #6b7280; font-size: 0.9em;">
                        ${constraints.join(' • ')}
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

async function saveConstraints() {
    const soldierId = document.getElementById('constraintSoldier').value;
    if (!soldierId) {
        showNotification('אנא בחר חייל', 'error');
        return;
    }

    const soldier = soldiers.find(s => s.id === soldierId);
    if (!soldier) return;

    // Get forbidden days (full days) from checkboxes
    const forbiddenDays = Array.from(document.querySelectorAll('.day-checkbox:checked'))
        .map(cb => parseInt(cb.value))
        .filter(d => !isNaN(d) && d >= 0 && d <= 6);

    // Get forbidden hours by day from checkboxes
    const forbiddenHoursByDay = {};
    document.querySelectorAll('.hour-checkbox-day:checked').forEach(cb => {
        const day = cb.dataset.day;
        const hour = parseInt(cb.value);
        if (!isNaN(hour) && hour >= 0 && hour <= 23 && day !== undefined) {
            if (!forbiddenHoursByDay[day]) {
                forbiddenHoursByDay[day] = [];
            }
            forbiddenHoursByDay[day].push(hour);
        }
    });
    
    // Remove empty arrays
    Object.keys(forbiddenHoursByDay).forEach(day => {
        if (forbiddenHoursByDay[day].length === 0) {
            delete forbiddenHoursByDay[day];
        }
    });

    // Get forbidden positions from checkboxes
    const forbiddenPositions = Array.from(document.querySelectorAll('.forbidden-pos:checked'))
        .map(cb => cb.value);

    soldier.constraints = {
        forbiddenDaysOfWeek: forbiddenDays.length > 0 ? forbiddenDays : null,
        forbiddenHoursByDay: Object.keys(forbiddenHoursByDay).length > 0 ? forbiddenHoursByDay : null,
        forbiddenPositions: forbiddenPositions.length > 0 ? forbiddenPositions : null
    };

    try {
        const response = await fetch(`${API_BASE}/soldiers/${soldierId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(soldier)
        });

        if (response.ok) {
            const updated = await response.json();
            const index = soldiers.findIndex(s => s.id === soldierId);
            if (index !== -1) {
                soldiers[index] = updated;
            }
            
            // Reset form
            document.getElementById('constraintSoldier').value = '';
            document.querySelectorAll('.hour-checkbox').forEach(cb => cb.checked = false);
            document.querySelectorAll('.day-checkbox').forEach(cb => cb.checked = false);
            document.querySelectorAll('.forbidden-pos').forEach(cb => cb.checked = false);
            
            displayConstraints();
            showNotification('אילוצים נשמרו בהצלחה', 'success');
        } else {
            const error = await response.text();
            showNotification(`שגיאה: ${error}`, 'error');
        }
    } catch (error) {
        console.error('Error saving constraints:', error);
        showNotification('שגיאה בשמירת האילוצים', 'error');
    }
}

// Schedule Generation
async function generateSchedule() {
    const startDate = document.getElementById('startDate').value;
    const endDate = document.getElementById('endDate').value;
    const startHourInput = document.getElementById('startHour').value;
    const endHourInput = document.getElementById('endHour').value;

    if (!startDate || !endDate) {
        showNotification('אנא בחר תאריכי התחלה וסיום', 'error');
        return;
    }

    if (new Date(startDate) > new Date(endDate)) {
        showNotification('תאריך התחלה חייב להיות לפני תאריך סיום', 'error');
        return;
    }

    if (positions.length === 0) {
        showNotification('אנא הוסף עמדות תחילה', 'error');
        return;
    }

    if (soldiers.length === 0) {
        showNotification('אנא הוסף חיילים תחילה', 'error');
        return;
    }

    // Parse hours (null if empty, otherwise the hour value)
    const startHour = startHourInput === '' ? null : parseInt(startHourInput);
    const endHour = endHourInput === '' ? null : parseInt(endHourInput);
    
    if (startHour !== null && (startHour < 0 || startHour > 23)) {
        showNotification('שעת התחלה חייבת להיות בין 0 ל-23', 'error');
        return;
    }
    
    if (endHour !== null && (endHour < 0 || endHour > 23)) {
        showNotification('שעת סיום חייבת להיות בין 0 ל-23', 'error');
        return;
    }

    const statusDiv = document.getElementById('scheduleStatus');
    statusDiv.innerHTML = '<p>יוצר לוח זמנים עם מרווח מקסימלי בין משמרות...</p>';
    statusDiv.className = '';

    const requestData = { 
        startDate, 
        endDate,
        startHour: startHour,
        endHour: endHour
    };
    
    console.log('=== יצירת לוח זמנים - התחלה ===');
    console.log('נתוני בקשה:', requestData);
    console.log('מספר עמדות:', positions.length);
    console.log('מספר חיילים:', soldiers.length);

    try {
        const response = await fetch(`${API_BASE}/schedule/generate`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestData)
        });

        console.log('תגובת שרת - סטטוס:', response.status, response.statusText);

        if (response.ok) {
            schedule = await response.json();
            console.log('לוח זמנים התקבל:', schedule);
            console.log('מספר משמרות בלוח:', schedule.length);
            if (schedule.length > 0) {
                console.log('משמרת ראשונה:', schedule[0]);
                console.log('מספר שיבוצים במשמרת ראשונה:', schedule[0].assignments?.length || 0);
            }
            
            statusDiv.innerHTML = '<p class="success">לוח זמנים נוצר בהצלחה!</p>';
            statusDiv.className = 'success';
            
            // Switch to view tab and display schedule
            setTimeout(() => {
                console.log('עובר לטאב לוח זמנים...');
                const viewTab = document.querySelector('[data-tab="view"]');
                if (viewTab) {
                    viewTab.click();
                    console.log('קורא ל-displaySchedule...');
                    displaySchedule();
                } else {
                    console.error('לא נמצא טאב לוח זמנים!');
                }
            }, 500);
        } else {
            const error = await response.text();
            console.error('שגיאה מהשרת:', error);
            statusDiv.innerHTML = `<p class="error">${error}</p>`;
            statusDiv.className = 'error';
        }
    } catch (error) {
        console.error('שגיאה ביצירת לוח זמנים:', error);
        statusDiv.innerHTML = '<p class="error">שגיאה ביצירת לוח זמנים</p>';
        statusDiv.className = 'error';
    }
    
    console.log('=== יצירת לוח זמנים - סיום ===');
}

async function loadSchedule() {
    try {
        const response = await fetch(`${API_BASE}/schedule`);
        if (response.ok) {
            schedule = await response.json();
            displaySchedule();
        }
    } catch (error) {
        console.error('Error loading schedule:', error);
    }
}

function displaySchedule() {
    console.log('=== displaySchedule - התחלה ===');
    console.log('לוח זמנים נוכחי:', schedule);
    console.log('מספר משמרות:', schedule.length);
    
    const view = document.getElementById('scheduleView');
    if (!view) {
        console.error('לא נמצא אלמנט scheduleView!');
        return;
    }
    
    if (schedule.length === 0) {
        console.log('לוח זמנים ריק - מציג הודעת ריק');
        view.innerHTML = `
            <div class="empty-state">
                <p>אין לוח זמנים</p>
                <p style="font-size: 0.9em; margin-top: 10px; color: #9ca3af;">צור לוח זמנים חדש בלשונית "תזמון"</p>
            </div>
        `;
        return;
    }
    
    console.log('מתחיל לבנות טבלה...');

    // Get all unique dates and positions
    const datesSet = new Set();
    const positionsMap = new Map();
    const soldiersMap = new Map();
    
    schedule.forEach(item => {
        if (!item) {
            console.warn('פריט לוח זמנים ריק:', item);
            return;
        }
        
        const date = item.date || item.Date;
        if (date) {
            datesSet.add(date);
        }
        
        const assignments = item.assignments || item.Assignments || [];
        assignments.forEach(a => {
            if (!a) return;
            const positionId = a.positionId || a.PositionId;
            const positionName = a.positionName || a.PositionName;
            const soldierId = a.soldierId || a.SoldierId;
            const soldierName = a.soldierName || a.SoldierName;
            
            if (positionId && !positionsMap.has(positionId)) {
                positionsMap.set(positionId, { id: positionId, name: positionName || '' });
            }
            if (soldierId && !soldiersMap.has(soldierId)) {
                soldiersMap.set(soldierId, { id: soldierId, name: soldierName || '' });
            }
        });
    });
    
    const allDates = Array.from(datesSet).sort();
    const allPositions = Array.from(positionsMap.values());
    const allSoldiers = Array.from(soldiersMap.values());
    
    // Generate pastel colors for positions
    const positionColors = {};
    const pastelPositionColors = [
        '#fce7f3', // Pink pastel
        '#e0e7ff', // Blue pastel
        '#fef3c7', // Yellow pastel
        '#d1fae5', // Green pastel
        '#fde68a', // Light yellow pastel
        '#e9d5ff', // Purple pastel
        '#fed7aa', // Orange pastel
        '#bae6fd', // Light blue pastel
        '#c7d2fe', // Indigo pastel
        '#fecdd3'  // Rose pastel
    ];
    allPositions.forEach((pos, index) => {
        positionColors[pos.id] = pastelPositionColors[index % pastelPositionColors.length];
    });
    
    // Calculate statistics for each soldier
    const soldierStats = calculateSoldierStats(schedule, allSoldiers);
    
    // Generate colors for soldiers
    const soldierColors = {};
    const soldierColorPalette = [
        '#3b82f6', // Blue
        '#ef4444', // Red
        '#10b981', // Green
        '#f59e0b', // Amber
        '#8b5cf6', // Purple
        '#ec4899', // Pink
        '#06b6d4', // Cyan
        '#84cc16', // Lime
        '#f97316', // Orange
        '#6366f1', // Indigo
        '#14b8a6', // Teal
        '#a855f7', // Violet
        '#22c55e', // Emerald
        '#eab308', // Yellow
        '#06b6d4', // Sky
        '#d946ef'  // Fuchsia
    ];
    allSoldiers.forEach((soldier, index) => {
        soldierColors[soldier.id] = soldierColorPalette[index % soldierColorPalette.length];
    });
    
    // Build hour-based schedule map: date -> position -> hour -> soldier
    const scheduleByHour = {};
    allDates.forEach(date => {
        scheduleByHour[date] = {};
        allPositions.forEach(pos => {
            scheduleByHour[date][pos.id] = {};
        });
    });
    
    schedule.forEach(item => {
        if (!item) return;
        
        const startTime = new Date(item.start || item.Start);
        const endTime = new Date(item.end || item.End);
        
        const assignments = item.assignments || item.Assignments || [];
        assignments.forEach(assignment => {
            if (!assignment) return;
            const posId = assignment.positionId || assignment.PositionId;
            
            // Calculate all hours in the shift
            let currentTime = new Date(startTime);
            
            while (currentTime < endTime) {
                const currentDate = currentTime.toISOString().split('T')[0];
                const currentHour = currentTime.getHours();
                
                if (!scheduleByHour[currentDate]) {
                    scheduleByHour[currentDate] = {};
                }
                if (!scheduleByHour[currentDate][posId]) {
                    scheduleByHour[currentDate][posId] = {};
                }
                scheduleByHour[currentDate][posId][currentHour] = assignment;
                
                // Move to next hour
                currentTime.setHours(currentTime.getHours() + 1);
            }
        });
    });
    
    // Create statistics section
    let statsHTML = '<div class="schedule-stats">';
    statsHTML += '<h3>סטטיסטיקות חיילים</h3>';
    statsHTML += '<div class="stats-grid">';
    
    allSoldiers.forEach(soldier => {
        const stats = soldierStats[soldier.id] || { totalShifts: 0, guardShifts: 0, avgGap: 0, maxGap: 0 };
        const color = soldierColors[soldier.id];
        
        statsHTML += `
            <div class="stat-card" style="border-right: 4px solid ${color};">
                <div class="stat-soldier-name" style="color: ${color}; font-weight: 600;">
                    ${soldier.name}
                </div>
                <div class="stat-details">
                    <div>סה"כ משמרות: <strong>${stats.totalShifts}</strong></div>
                    <div>שמירות: <strong>${stats.guardShifts || 0}</strong></div>
                    <div>רווח ממוצע: <strong>${stats.avgGap.toFixed(1)}</strong> שעות</div>
                    <div>רווח מקסימלי: <strong>${stats.maxGap}</strong> שעות</div>
                </div>
            </div>
        `;
    });
    
    statsHTML += '</div></div>';
    
    // Group schedule by date and shift
    const scheduleByDateAndShift = {};
    schedule.forEach(item => {
        if (!item) return;
        
        const date = item.date || item.Date;
        const shiftNumber = item.shiftNumber !== undefined ? item.shiftNumber : item.ShiftNumber;
        
        if (!date || shiftNumber === undefined) {
            console.warn('פריט ללא תאריך או מספר משמרת:', item);
            return;
        }
        
        if (!scheduleByDateAndShift[date]) {
            scheduleByDateAndShift[date] = {};
        }
        if (!scheduleByDateAndShift[date][shiftNumber]) {
            scheduleByDateAndShift[date][shiftNumber] = item;
        }
    });
    
    // Create table by shifts (like the image format)
    let tableHTML = '<div style="overflow-x: auto; margin-top: 30px;"><table class="schedule-table-shifts">';
    
    // Header row
    tableHTML += '<thead><tr>';
    tableHTML += '<th class="day-col">יום</th>';
    tableHTML += '<th class="time-col">שעה</th>';
    allPositions.forEach(pos => {
        tableHTML += `<th class="position-col">${pos.name}</th>`;
    });
    tableHTML += '</tr></thead><tbody>';
    
    // Data rows - grouped by date, then by shift
    allDates.forEach(date => {
        const dateObj = new Date(date);
        const dayName = dateObj.toLocaleDateString('he-IL', { weekday: 'long' });
        const dateStr = dateObj.toLocaleDateString('he-IL', { day: '2-digit', month: '2-digit' });
        
        const shifts = Object.keys(scheduleByDateAndShift[date] || {})
            .map(n => parseInt(n))
            .sort((a, b) => a - b);
        
        shifts.forEach((shiftNum, index) => {
            const shift = scheduleByDateAndShift[date][shiftNum];
            if (!shift) {
                console.warn(`משמרת ${shiftNum} לא נמצאה ליום ${date}`);
                return;
            }
            
            const start = shift.start || shift.Start;
            const end = shift.end || shift.End;
            const assignments = shift.assignments || shift.Assignments || [];
            
            if (!start || !end) {
                console.warn(`משמרת ${shiftNum} ללא תאריכי התחלה/סיום:`, shift);
                return;
            }
            
            const startTime = new Date(start).toLocaleTimeString('he-IL', { 
                hour: '2-digit', 
                minute: '2-digit' 
            });
            const endTime = new Date(end).toLocaleTimeString('he-IL', { 
                hour: '2-digit', 
                minute: '2-digit' 
            });
            
            tableHTML += `<tr>`;
            
            // Day column (merged for first shift of the day)
            if (index === 0) {
                tableHTML += `<td class="day-cell" rowspan="${shifts.length}">${dayName}<br><small>${dateStr}</small></td>`;
            }
            
            // Time column
            tableHTML += `<td class="time-cell">${startTime}-${endTime}</td>`;
            
            // Position columns
            allPositions.forEach(pos => {
                const assignment = assignments.find(a => {
                    const aPosId = a.positionId || a.PositionId;
                    return aPosId === pos.id;
                });
                const positionColor = positionColors[pos.id] || '#ffffff';
                if (assignment) {
                    const soldierId = assignment.soldierId || assignment.SoldierId || '';
                    const soldierName = assignment.soldierName || assignment.SoldierName || '';
                    const editClass = editMode ? 'editable-cell' : '';
                    const shiftNumber = shift.shiftNumber !== undefined ? shift.shiftNumber : shift.ShiftNumber;
                    const onClick = editMode ? `onclick="openEditCell('${date}', ${shiftNumber}, '${pos.id}', '${soldierId}', '${soldierName}', '${pos.name}')"` : `onclick="highlightSoldier('${soldierId}')"`;
                    tableHTML += `<td class="soldier-cell ${editClass}" data-soldier-id="${soldierId}" style="background-color: ${positionColor};" title="${soldierName}" ${onClick}>${soldierName}</td>`;
                } else {
                    const editClass = editMode ? 'editable-cell' : '';
                    const startHour = new Date(start).getHours();
                    const shiftNumber = shift.shiftNumber !== undefined ? shift.shiftNumber : shift.ShiftNumber;
                    const onClick = editMode ? `onclick="openEditCell('${date}', ${shiftNumber}, '${pos.id}', '', '', '${pos.name}')"` : '';
                    tableHTML += `<td class="empty-cell ${editClass}" style="background-color: ${positionColor};" ${onClick}>-</td>`;
                }
            });
            
            tableHTML += `</tr>`;
        });
    });
    
    tableHTML += '</tbody></table></div>';
    console.log('מציג טבלה...');
    console.log('אורך statsHTML:', statsHTML.length);
    console.log('אורך tableHTML:', tableHTML.length);
    // מציגים קודם את הטבלה ואז את הסטטיסטיקות
    view.innerHTML = tableHTML + statsHTML;
    console.log('טבלה הוצגה בהצלחה');
    console.log('=== displaySchedule - סיום ===');
}

function calculateSoldierStats(schedule, allSoldiers) {
    const stats = {};
    
    allSoldiers.forEach(soldier => {
        const soldierShifts = [];
        
        // Collect all shifts for this soldier
        let guardShiftsCount = 0; // ספירת שמירות בלבד (לא כוננות)
        
        schedule.forEach(item => {
            const assignments = item.assignments || item.Assignments || [];
            assignments.forEach(assignment => {
                if (!assignment) return;
                const soldierId = assignment.soldierId || assignment.SoldierId;
                if (soldierId === soldier.id) {
                    // בדוק אם זו עמדת שמירה (לא כוננות)
                    const positionId = assignment.positionId || assignment.PositionId;
                    const position = positions.find(p => p.id === positionId);
                    if (position && !position.isStandby) {
                        guardShiftsCount++;
                    }
                    
                    soldierShifts.push({
                        date: item.date || item.Date,
                        start: new Date(item.start || item.Start),
                        end: new Date(item.end || item.End),
                        positionId: positionId,
                        isStandby: position ? position.isStandby : false
                    });
                }
            });
        });
        
        // Sort by start time
        soldierShifts.sort((a, b) => a.start - b.start);
        
        if (soldierShifts.length === 0) {
            stats[soldier.id] = {
                totalShifts: 0,
                guardShifts: 0,
                avgGap: 0,
                maxGap: 0
            };
            return;
        }
        
        // Calculate gaps between shifts
        const gaps = [];
        for (let i = 1; i < soldierShifts.length; i++) {
            const prevEnd = soldierShifts[i - 1].end;
            const currStart = soldierShifts[i].start;
            const gapHours = (currStart - prevEnd) / (1000 * 60 * 60);
            gaps.push(gapHours);
        }
        
        // Also check gap from last shift to first shift (wrap around)
        if (soldierShifts.length > 1) {
            const lastShift = soldierShifts[soldierShifts.length - 1];
            const firstShift = soldierShifts[0];
            const lastDate = new Date(lastShift.date);
            const firstDate = new Date(firstShift.date);
            const daysDiff = (firstDate - lastDate) / (1000 * 60 * 60 * 24);
            if (daysDiff > 0) {
                const wrapGap = ((firstShift.start - lastShift.end) / (1000 * 60 * 60)) + (daysDiff * 24);
                gaps.push(wrapGap);
            }
        }
        
        const avgGap = gaps.length > 0 ? gaps.reduce((a, b) => a + b, 0) / gaps.length : 0;
        const maxGap = gaps.length > 0 ? Math.max(...gaps) : 0;
        
        stats[soldier.id] = {
            totalShifts: soldierShifts.length,
            guardShifts: guardShiftsCount, // מספר שמירות בלבד
            avgGap: avgGap,
            maxGap: Math.round(maxGap)
        };
    });
    
    return stats;
}


// Notification system
function showNotification(message, type = 'info') {
    // Remove existing notifications
    const existing = document.querySelector('.notification');
    if (existing) existing.remove();

    const notification = document.createElement('div');
    notification.className = `notification notification-${type}`;
    notification.textContent = message;
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        left: 50%;
        transform: translateX(-50%);
        padding: 16px 24px;
        border-radius: 12px;
        box-shadow: 0 10px 25px rgba(0,0,0,0.2);
        z-index: 10000;
        animation: slideDown 0.3s ease-out;
        font-weight: 500;
        max-width: 90%;
    `;

    if (type === 'success') {
        notification.style.background = 'linear-gradient(135deg, #10b981 0%, #059669 100%)';
        notification.style.color = 'white';
    } else if (type === 'error') {
        notification.style.background = 'linear-gradient(135deg, #ef4444 0%, #dc2626 100%)';
        notification.style.color = 'white';
    } else if (type === 'warning') {
        notification.style.background = 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)';
        notification.style.color = 'white';
    } else {
        notification.style.background = 'linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)';
        notification.style.color = 'white';
    }

    document.body.appendChild(notification);

    setTimeout(() => {
        notification.style.animation = 'slideUp 0.3s ease-out';
        setTimeout(() => notification.remove(), 300);
    }, 3000);
}

// Add CSS for notifications
const style = document.createElement('style');
style.textContent = `
    @keyframes slideDown {
        from {
            opacity: 0;
            transform: translateX(-50%) translateY(-20px);
        }
        to {
            opacity: 1;
            transform: translateX(-50%) translateY(0);
        }
    }
    @keyframes slideUp {
        from {
            opacity: 1;
            transform: translateX(-50%) translateY(0);
        }
        to {
            opacity: 0;
            transform: translateX(-50%) translateY(-20px);
        }
    }
`;
document.head.appendChild(style);

// Highlight soldier function
let highlightedSoldierId = null;

function highlightSoldier(soldierId) {
    if (!soldierId) return;
    
    // Remove previous highlights
    document.querySelectorAll('.soldier-cell.highlighted').forEach(cell => {
        cell.classList.remove('highlighted');
    });
    
    // If clicking the same soldier, unhighlight
    if (highlightedSoldierId === soldierId) {
        highlightedSoldierId = null;
        return;
    }
    
    // Highlight all cells with this soldier
    highlightedSoldierId = soldierId;
    document.querySelectorAll(`.soldier-cell[data-soldier-id="${soldierId}"]`).forEach(cell => {
        cell.classList.add('highlighted');
    });
}

// Edit Mode Functions
function openEditCell(date, shiftNumber, positionId, currentSoldierId, currentSoldierName, positionName) {
    const modal = document.getElementById('editModal');
    const modalTitle = document.getElementById('editModalTitle');
    const modalContent = document.getElementById('editModalContent');
    
    if (!modal || !modalTitle || !modalContent) return;
    
    const position = positions.find(p => p.id === positionId);
    const posName = positionName || (position ? position.name : 'עמדה לא ידועה');
    
    modalTitle.textContent = `עריכת שיבוץ - ${posName}`;
    
    // Build edit options
    let editHTML = `
        <div class="edit-options">
            <h4>בחר פעולה:</h4>
            <div class="edit-option-buttons">
                <button class="btn-primary" onclick="openReplaceSingleShift('${date}', ${shiftNumber}, '${positionId}', '${posName}', '${currentSoldierId || ''}', '${currentSoldierName || ''}')">
                    החלף חייל בשעה זו בלבד
                </button>
                ${currentSoldierId ? `
                <button class="btn-primary" onclick="openSwapAllShifts('${currentSoldierId}', '${currentSoldierName}')">
                    החלף חייל בכל המשמרות שלו
                </button>
                ` : ''}
            </div>
        </div>
    `;
    
    modalContent.innerHTML = editHTML;
    modal.style.display = 'flex';
}

function closeEditModal() {
    const modal = document.getElementById('editModal');
    if (modal) {
        modal.style.display = 'none';
    }
}

function openReplaceSingleShift(date, shiftNumber, positionId, positionName, oldSoldierId, oldSoldierName) {
    const modalContent = document.getElementById('editModalContent');
    if (!modalContent) return;
    
    let selectHTML = '<label class="input-label"><span class="label-text">בחר חייל חדש:</span><select id="newSoldierSelect" class="modern-input">';
    selectHTML += '<option value="">-- בחר חייל --</option>';
    soldiers.forEach(soldier => {
        if (soldier.id !== oldSoldierId) {
            selectHTML += `<option value="${soldier.id}">${soldier.name}</option>`;
        }
    });
    selectHTML += '</select></label>';
    
    modalContent.innerHTML = `
        <div class="edit-form">
            <p>מחליף חייל בשעה זו בלבד</p>
            <p><strong>עמדה:</strong> ${positionName}</p>
            <p><strong>חייל נוכחי:</strong> ${oldSoldierName || 'ריק'}</p>
            ${selectHTML}
            <div class="form-actions" style="margin-top: 20px;">
                <button class="btn-primary" onclick="replaceSoldierInShift('${date}', ${shiftNumber}, '${positionId}', '${positionName}', '${oldSoldierId || ''}')">
                    החלף
                </button>
                <button class="btn-secondary" onclick="closeEditModal()">ביטול</button>
            </div>
        </div>
    `;
}

function openSwapAllShifts(soldier1Id, soldier1Name) {
    const modalContent = document.getElementById('editModalContent');
    if (!modalContent) return;
    
    let selectHTML = '<label class="input-label"><span class="label-text">בחר חייל להחלפה:</span><select id="swapSoldierSelect" class="modern-input">';
    selectHTML += '<option value="">-- בחר חייל --</option>';
    soldiers.forEach(soldier => {
        if (soldier.id !== soldier1Id) {
            selectHTML += `<option value="${soldier.id}">${soldier.name}</option>`;
        }
    });
    selectHTML += '</select></label>';
    
    modalContent.innerHTML = `
        <div class="edit-form">
            <p>מחליף את כל המשמרות של <strong>${soldier1Name}</strong> עם חייל אחר</p>
            ${selectHTML}
            <div class="form-actions" style="margin-top: 20px;">
                <button class="btn-primary" onclick="swapSoldiers('${soldier1Id}', '${soldier1Name}')">
                    החלף בכל המשמרות
                </button>
                <button class="btn-secondary" onclick="closeEditModal()">ביטול</button>
            </div>
        </div>
    `;
}

async function replaceSoldierInShift(date, shiftNumber, positionId, positionName, oldSoldierId) {
    const newSoldierSelect = document.getElementById('newSoldierSelect');
    if (!newSoldierSelect || !newSoldierSelect.value) {
        showNotification('אנא בחר חייל חדש', 'error');
        return;
    }
    
    const newSoldierId = newSoldierSelect.value;
    const newSoldier = soldiers.find(s => s.id === newSoldierId);
    if (!newSoldier) {
        showNotification('חייל לא נמצא', 'error');
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/schedule/replace`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                date: date,
                shiftNumber: shiftNumber,
                positionId: positionId,
                positionName: positionName,
                oldSoldierId: oldSoldierId || '',
                newSoldierId: newSoldierId,
                newSoldierName: newSoldier.name
            })
        });
        
        if (response.ok) {
            schedule = await response.json();
            closeEditModal();
            displaySchedule();
            showNotification('שיבוץ עודכן בהצלחה', 'success');
        } else {
            const error = await response.text();
            showNotification(`שגיאה: ${error}`, 'error');
        }
    } catch (error) {
        console.error('Error replacing soldier:', error);
        showNotification('שגיאה בהחלפת חייל', 'error');
    }
}

async function swapSoldiers(soldier1Id, soldier1Name) {
    const swapSoldierSelect = document.getElementById('swapSoldierSelect');
    if (!swapSoldierSelect || !swapSoldierSelect.value) {
        showNotification('אנא בחר חייל להחלפה', 'error');
        return;
    }
    
    const soldier2Id = swapSoldierSelect.value;
    const soldier2 = soldiers.find(s => s.id === soldier2Id);
    if (!soldier2) {
        showNotification('חייל לא נמצא', 'error');
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/schedule/swap`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                soldier1Id: soldier1Id,
                soldier1Name: soldier1Name,
                soldier2Id: soldier2Id,
                soldier2Name: soldier2.name
            })
        });
        
        if (response.ok) {
            schedule = await response.json();
            closeEditModal();
            displaySchedule();
            showNotification('חיילים הוחלפו בהצלחה בכל המשמרות', 'success');
        } else {
            const error = await response.text();
            showNotification(`שגיאה: ${error}`, 'error');
        }
    } catch (error) {
        console.error('Error swapping soldiers:', error);
        showNotification('שגיאה בהחלפת חיילים', 'error');
    }
}

// Close modal when clicking outside
document.addEventListener('click', (e) => {
    const modal = document.getElementById('editModal');
    if (modal && e.target === modal) {
        closeEditModal();
    }
});

// Saved Schedules Management
let savedSchedules = [];

async function loadSavedSchedules() {
    try {
        const response = await fetch(`${API_BASE}/savedSchedules`);
        if (response.ok) {
            savedSchedules = await response.json();
            displaySavedSchedules();
            loadSharedSchedules();
        }
    } catch (error) {
        console.error('Error loading saved schedules:', error);
    }
}

function displaySavedSchedules() {
    const list = document.getElementById('savedSchedulesList');
    if (!list) return;
    
    if (savedSchedules.length === 0) {
        list.innerHTML = '<p style="color: #9ca3af; text-align: center; padding: 20px;">אין לוחות זמנים שמורים</p>';
        return;
    }
    
    list.innerHTML = savedSchedules.map(s => {
        const createdDate = new Date(s.createdAt || s.CreatedAt).toLocaleDateString('he-IL');
        const modifiedDate = s.lastModified || s.LastModified 
            ? new Date(s.lastModified || s.LastModified).toLocaleDateString('he-IL')
            : null;
        
        return `
            <div class="saved-schedule-item" style="border: 1px solid #e5e7eb; border-radius: 8px; padding: 15px; margin-bottom: 15px;">
                <div style="display: flex; justify-content: space-between; align-items: start;">
                    <div style="flex: 1;">
                        <h4 style="margin: 0 0 10px 0; color: #1f2937;">${s.name || s.Name}</h4>
                        ${s.description || s.Description ? `<p style="margin: 0 0 10px 0; color: #6b7280; font-size: 0.9em;">${s.description || s.Description}</p>` : ''}
                        <div style="font-size: 0.85em; color: #9ca3af;">
                            <div>נוצר: ${createdDate} ${s.createdBy || s.CreatedBy ? `על ידי ${s.createdBy || s.CreatedBy}` : ''}</div>
                            ${modifiedDate ? `<div>עודכן: ${modifiedDate}</div>` : ''}
                            ${s.isShared || s.IsShared ? `<div style="color: #3b82f6; margin-top: 5px;">🔗 משותף - קוד: ${s.shareCode || s.ShareCode}</div>` : ''}
                        </div>
                    </div>
                    <div style="display: flex; gap: 10px; flex-direction: column;">
                        <button class="btn-primary" onclick="loadSavedSchedule('${s.id || s.Id}')" style="white-space: nowrap;">
                            טען
                        </button>
                        <button class="btn-secondary" onclick="toggleShareSchedule('${s.id || s.Id}', ${s.isShared || s.IsShared})" style="white-space: nowrap;">
                            ${s.isShared || s.IsShared ? 'בטל שיתוף' : 'שתף'}
                        </button>
                        <button class="btn-danger" onclick="deleteSavedSchedule('${s.id || s.Id}')" style="white-space: nowrap;">
                            מחק
                        </button>
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

async function loadSharedSchedules() {
    try {
        const response = await fetch(`${API_BASE}/savedSchedules/shared`);
        if (response.ok) {
            const shared = await response.json();
            const list = document.getElementById('sharedSchedulesList');
            if (!list) return;
            
            if (shared.length === 0) {
                list.innerHTML = '<p style="color: #9ca3af; text-align: center; padding: 20px;">אין לוחות זמנים משותפים</p>';
                return;
            }
            
            list.innerHTML = shared.map(s => {
                const createdDate = new Date(s.createdAt || s.CreatedAt).toLocaleDateString('he-IL');
                return `
                    <div class="shared-schedule-item" style="border: 1px solid #3b82f6; border-radius: 8px; padding: 15px; margin-bottom: 15px; background: #eff6ff;">
                        <div style="display: flex; justify-content: space-between; align-items: start;">
                            <div style="flex: 1;">
                                <h4 style="margin: 0 0 10px 0; color: #1f2937;">${s.name || s.Name}</h4>
                                ${s.description || s.Description ? `<p style="margin: 0 0 10px 0; color: #6b7280; font-size: 0.9em;">${s.description || s.Description}</p>` : ''}
                                <div style="font-size: 0.85em; color: #9ca3af;">
                                    <div>נוצר: ${createdDate} ${s.createdBy || s.CreatedBy ? `על ידי ${s.createdBy || s.CreatedBy}` : ''}</div>
                                    <div style="color: #3b82f6; margin-top: 5px; font-weight: 600;">קוד שיתוף: ${s.shareCode || s.ShareCode}</div>
                                </div>
                            </div>
                            <button class="btn-primary" onclick="loadSharedScheduleByCode('${s.shareCode || s.ShareCode}')" style="white-space: nowrap;">
                                טען
                            </button>
                        </div>
                    </div>
                `;
            }).join('');
        }
    } catch (error) {
        console.error('Error loading shared schedules:', error);
    }
}

async function saveCurrentSchedule() {
    if (schedule.length === 0) {
        showNotification('אין לוח זמנים לשמירה', 'error');
        return;
    }
    
    const name = document.getElementById('scheduleName').value.trim();
    if (!name) {
        showNotification('אנא הזן שם ללוח זמנים', 'error');
        return;
    }
    
    const description = document.getElementById('scheduleDescription').value.trim();
    const isShared = document.getElementById('scheduleIsShared').checked;
    
    try {
        const response = await fetch(`${API_BASE}/savedSchedules`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                name: name,
                description: description,
                createdBy: 'משתמש',
                schedule: schedule,
                isShared: isShared
            })
        });
        
        if (response.ok) {
            const saved = await response.json();
            showNotification('לוח זמנים נשמר בהצלחה!', 'success');
            document.getElementById('scheduleName').value = '';
            document.getElementById('scheduleDescription').value = '';
            document.getElementById('scheduleIsShared').checked = false;
            
            if (isShared && saved.shareCode) {
                showNotification(`לוח זמנים נשמר ושותף! קוד שיתוף: ${saved.shareCode}`, 'success');
            }
            
            loadSavedSchedules();
        } else {
            const error = await response.text();
            showNotification(`שגיאה: ${error}`, 'error');
        }
    } catch (error) {
        console.error('Error saving schedule:', error);
        showNotification('שגיאה בשמירת לוח זמנים', 'error');
    }
}

async function loadSavedSchedule(id) {
    try {
        const response = await fetch(`${API_BASE}/savedSchedules/${id}`);
        if (response.ok) {
            const saved = await response.json();
            schedule = saved.schedule || saved.Schedule || [];
            showNotification('לוח זמנים נטען בהצלחה!', 'success');
            
            // Switch to view tab and display
            setTimeout(() => {
                document.querySelector('[data-tab="view"]').click();
                displaySchedule();
            }, 500);
        } else {
            showNotification('שגיאה בטעינת לוח זמנים', 'error');
        }
    } catch (error) {
        console.error('Error loading schedule:', error);
        showNotification('שגיאה בטעינת לוח זמנים', 'error');
    }
}

async function loadSharedSchedule() {
    const shareCode = document.getElementById('shareCodeInput').value.trim().toUpperCase();
    if (!shareCode || shareCode.length !== 6) {
        showNotification('אנא הזן קוד שיתוף תקין (6 תווים)', 'error');
        return;
    }
    
    await loadSharedScheduleByCode(shareCode);
}

async function loadSharedScheduleByCode(shareCode) {
    try {
        const response = await fetch(`${API_BASE}/savedSchedules/share/${shareCode}`);
        if (response.ok) {
            const saved = await response.json();
            schedule = saved.schedule || saved.Schedule || [];
            showNotification('לוח זמנים משותף נטען בהצלחה!', 'success');
            document.getElementById('shareCodeInput').value = '';
            
            // Switch to view tab and display
            setTimeout(() => {
                document.querySelector('[data-tab="view"]').click();
                displaySchedule();
            }, 500);
        } else {
            showNotification('לוח זמנים משותף לא נמצא', 'error');
        }
    } catch (error) {
        console.error('Error loading shared schedule:', error);
        showNotification('שגיאה בטעינת לוח זמנים משותף', 'error');
    }
}

async function toggleShareSchedule(id, currentShareStatus) {
    try {
        const response = await fetch(`${API_BASE}/savedSchedules/${id}/share`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                isShared: !currentShareStatus
            })
        });
        
        if (response.ok) {
            const updated = await response.json();
            showNotification(
                updated.isShared 
                    ? `לוח זמנים משותף! קוד שיתוף: ${updated.shareCode}` 
                    : 'שיתוף לוח זמנים בוטל',
                'success'
            );
            loadSavedSchedules();
        } else {
            showNotification('שגיאה בעדכון שיתוף', 'error');
        }
    } catch (error) {
        console.error('Error toggling share:', error);
        showNotification('שגיאה בעדכון שיתוף', 'error');
    }
}

async function deleteSavedSchedule(id) {
    if (!confirm('האם אתה בטוח שברצונך למחוק לוח זמנים זה?')) {
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/savedSchedules/${id}`, {
            method: 'DELETE'
        });
        
        if (response.ok) {
            showNotification('לוח זמנים נמחק בהצלחה', 'success');
            loadSavedSchedules();
        } else {
            showNotification('שגיאה במחיקת לוח זמנים', 'error');
        }
    } catch (error) {
        console.error('Error deleting schedule:', error);
        showNotification('שגיאה במחיקת לוח זמנים', 'error');
    }
}
