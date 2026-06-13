const PASS_SCROLL_SELECTOR = '.pass-scroll-to-main';
const TAP_THRESHOLD_PX = 8;

const MOMENTUM_FRICTION = 0.94;
const MOMENTUM_MAX_VELOCITY = 30;
const MOMENTUM_MIN_VELOCITY = 0.5;
const MOMENTUM_MAX_DURATION_MS = 1500;
const MOMENTUM_SAMPLE_WINDOW_MS = 60;

// Сохраняем глобальный объект для внешних скриптов
const passScrollState = { wasDrag: false };

function passScrollToMain(el) {
    // Безопасный динамический поиск целевого элемента
    const getMainElement = () => document.querySelector('main') || document.scrollingElement;
    
    let pendingDelta = 0;
    let frameScheduled = false;
    let previousTouchY = 0;
    let dragDistance = 0;
    let velocity = 0;
    let momentumStart = 0;
    let momentumFrameId = null; 
    let primaryTouchId = null;  
    const samples = [];

    function flushDelta() {
        const target = getMainElement();
        if (target) {
            target.scrollBy(0, pendingDelta);
        }
        pendingDelta = 0;
        frameScheduled = false;
    }

    function enqueueDelta(amount) {
        pendingDelta += amount;
        if (!frameScheduled) {
            frameScheduled = true;
            requestAnimationFrame(flushDelta);
        }
    }

    function sampleTouch(y, t) {
        samples.push({ y, t });
        // ИСПРАВЛЕНО: добавлен индекс, чтобы правильно проверять первый элемент массива
        while (samples.length > 0 && t - samples[0].t > MOMENTUM_SAMPLE_WINDOW_MS) {
            samples.shift();
        }
    }

    function currentVelocity() {
        if (samples.length < 2) return 0;
        const first = samples[0];
        const last = samples[samples.length - 1];
        const dt = last.t - first.t;
        if (dt <= 0) return 0;
        return ((first.y - last.y) / dt) * 16.6; 
    }

    function stopMomentum() {
        velocity = 0;
        if (momentumFrameId) {
            cancelAnimationFrame(momentumFrameId);
            momentumFrameId = null;
        }
    }

    function runMomentumFrame() {
        if (Math.abs(velocity) < MOMENTUM_MIN_VELOCITY) return;
        if (performance.now() - momentumStart > MOMENTUM_MAX_DURATION_MS) return;

        enqueueDelta(velocity);
        velocity *= MOMENTUM_FRICTION;
        momentumFrameId = requestAnimationFrame(runMomentumFrame);
    }

    el.addEventListener('wheel', (event) => {
        enqueueDelta(event.deltaY);
    }, { passive: true });

    el.addEventListener('touchstart', (event) => {
        const touch = event.changedTouches[0];
        primaryTouchId = touch.identifier; 
        previousTouchY = touch.clientY;
        dragDistance = 0;
        samples.length = 0;
        stopMomentum();
    }, { passive: true });

    el.addEventListener('touchmove', (event) => {
        const touch = Array.from(event.touches).find(t => t.identifier === primaryTouchId);
        if (!touch) return;

        const currentTouchY = touch.clientY;
        const delta = previousTouchY - currentTouchY;

        dragDistance += Math.abs(delta);
        if (dragDistance > TAP_THRESHOLD_PX) {
            passScrollState.wasDrag = true; // Выставляем глобальный флаг
        }

        enqueueDelta(delta);
        sampleTouch(currentTouchY, performance.now());

        previousTouchY = currentTouchY;
    }, { passive: true });

    function resetDragState() {
        // Увеличиваем задержку до 50мс. Это гарантирует, что нативный 
        // клик успеет заблокироваться внешним кодом до того, как флаг станет false.
        setTimeout(() => {
            passScrollState.wasDrag = false;
        }, 50);
        primaryTouchId = null;
    }

    function endTouch(event) {
        const touchEnded = Array.from(event.changedTouches).some(t => t.identifier === primaryTouchId);
        if (!touchEnded) return;

        velocity = Math.max(-MOMENTUM_MAX_VELOCITY, Math.min(MOMENTUM_MAX_VELOCITY, currentVelocity()));
        samples.length = 0;
        
        if (Math.abs(velocity) >= MOMENTUM_MIN_VELOCITY) {
            momentumStart = performance.now();
            momentumFrameId = requestAnimationFrame(runMomentumFrame);
        }
        
        resetDragState();
    }

    el.addEventListener('touchend', endTouch, { passive: true });
    el.addEventListener('touchcancel', (event) => {
        const touchCancelled = Array.from(event.changedTouches).some(t => t.identifier === primaryTouchId);
        if (!touchCancelled) return;

        stopMomentum();
        resetDragState();
    }, { passive: true });
}

document.querySelectorAll(PASS_SCROLL_SELECTOR).forEach(passScrollToMain);
