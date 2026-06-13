const PASS_SCROLL_SELECTOR = '.pass-scroll-to-main';
const TAP_THRESHOLD_PX = 8;

const MOMENTUM_FRICTION = 0.94;
const MOMENTUM_MAX_VELOCITY = 2.0; // Изменено: пиксели в миллисекунду
const MOMENTUM_MIN_VELOCITY = 0.03;
const MOMENTUM_MAX_DURATION_MS = 1500;
const MOMENTUM_SAMPLE_WINDOW_MS = 60;

const passScrollState = { wasDrag: false };

function passScrollToMain(el) {
    const getMainElement = () => document.querySelector('main') || document.scrollingElement;
    
    let startTouchY = 0;
    let startScrollY = 0;
    let previousTouchY = 0;
    let dragDistance = 0;
    
    let velocity = 0;
    let momentumStart = 0;
    let lastFrameTime = 0;
    let momentumFrameId = null; 
    let primaryTouchId = null;  
    const samples = [];

    function sampleTouch(y, t) {
        samples.push({ y, t });
        while (samples.length > 0 && t - samples[0].t > MOMENTUM_SAMPLE_WINDOW_MS) {
            samples.shift();
        }
    }

    function calculateVelocity() {
        if (samples.length < 2) return 0;
        const first = samples[0];
        const last = samples[samples.length - 1];
        const dt = last.t - first.t;
        if (dt <= 0) return 0;
        // Возвращает чистые px/ms (не привязано к герцовке)
        return (first.y - last.y) / dt; 
    }

    function stopMomentum() {
        velocity = 0;
        if (momentumFrameId) {
            cancelAnimationFrame(momentumFrameId);
            momentumFrameId = null;
        }
    }

    function runMomentumFrame(now) {
        if (Math.abs(velocity) < MOMENTUM_MIN_VELOCITY) return;
        if (now - momentumStart > MOMENTUM_MAX_DURATION_MS) return;

        // Вычисляем реальное время между кадрами (на 120Гц это ~8.3мс, на 60Гц — ~16.6мс)
        const dt = lastFrameTime ? now - lastFrameTime : 16.6;
        lastFrameTime = now;

        const target = getMainElement();
        if (target) {
            target.scrollBy(0, velocity * dt);
        }

        // Адаптивное затухание скорости под временной шаг
        velocity *= Math.pow(MOMENTUM_FRICTION, dt / 16.6);
        momentumFrameId = requestAnimationFrame(runMomentumFrame);
    }

    // Скролл колесиком мыши / тачпадом остается без изменений
    el.addEventListener('wheel', (event) => {
        const target = getMainElement();
        if (target) target.scrollBy(0, event.deltaY);
    }, { passive: true });

    el.addEventListener('touchstart', (event) => {
        const touch = event.changedTouches[0];
        primaryTouchId = touch.identifier; 
        
        startTouchY = touch.clientY;
        previousTouchY = touch.clientY;
        
        const target = getMainElement();
        startScrollY = target ? target.scrollTop : 0;
        
        dragDistance = 0;
        samples.length = 0;
        stopMomentum();
    }, { passive: true });

    el.addEventListener('touchmove', (event) => {
        const touch = Array.from(event.touches).find(t => t.identifier === primaryTouchId);
        if (!touch) return;

        const currentTouchY = touch.clientY;
        
        // Считаем общее смещение пальца от точки старта
        const totalDeltaY = startTouchY - currentTouchY;
        dragDistance = Math.abs(totalDeltaY);

        if (dragDistance > TAP_THRESHOLD_PX) {
            passScrollState.wasDrag = true;
        }

        // Перемещаем скролл строго вслед за пальцем (1:1), игнорируя частоту кадров
        const target = getMainElement();
        if (target) {
            target.scrollTop = startScrollY + totalDeltaY;
        }

        sampleTouch(currentTouchY, performance.now());
        previousTouchY = currentTouchY;
    }, { passive: true });

    function resetDragState() {
        setTimeout(() => {
            passScrollState.wasDrag = false;
        }, 50);
        primaryTouchId = null;
    }

    function endTouch(event) {
        const touchEnded = Array.from(event.changedTouches).some(t => t.identifier === primaryTouchId);
        if (!touchEnded) return;

        const rawVelocity = calculateVelocity();
        // Ограничиваем скорость в рамках px/ms
        velocity = Math.max(-MOMENTUM_MAX_VELOCITY, Math.min(MOMENTUM_MAX_VELOCITY, rawVelocity));
        samples.length = 0;
        
        if (Math.abs(velocity) >= MOMENTUM_MIN_VELOCITY) {
            momentumStart = performance.now();
            lastFrameTime = 0; // Сброс для первого кадра анимации
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
