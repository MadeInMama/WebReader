const PASS_SCROLL_SELECTOR = '.pass-scroll-to-main';
const TAP_THRESHOLD_PX = 8;

const MOMENTUM_FRICTION = 0.94;
const MOMENTUM_MAX_VELOCITY = 2.0;
const MOMENTUM_MIN_VELOCITY = 0.03;
const MOMENTUM_MAX_DURATION_MS = 1500;
const MOMENTUM_SAMPLE_WINDOW_MS = 60;

const passScrollState = {
    wasDrag: false
};

let startTouchY = 0;
let startScrollY = 0;
let dragDistance = 0;

let velocity = 0;
let momentumStart = 0;
let lastFrameTime = 0;
let momentumFrameId = null;
let primaryTouchId = null;
const samples = [];

function passScrollToMain(el) {
    const getMainElement = () => document.querySelector('main') || document.scrollingElement;

    function sampleTouch(y, t) {
        samples.push({y, t});
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

        const dt = lastFrameTime ? now - lastFrameTime : 16.6;
        lastFrameTime = now;

        const target = getMainElement();
        if (target) {
            target.scrollBy(0, velocity * dt);
        }

        velocity *= Math.pow(MOMENTUM_FRICTION, dt / 16.6);
        momentumFrameId = requestAnimationFrame(runMomentumFrame);
    }

    el.addEventListener('wheel', (event) => {
        event.preventDefault();
        const target = getMainElement();
        if (target) target.scrollBy(0, event.deltaY);
    });

    el.addEventListener('touchstart', (event) => {
        const touch = event.changedTouches[0];
        primaryTouchId = touch.identifier;

        startTouchY = touch.clientY;

        const target = getMainElement();
        startScrollY = target ? target.scrollTop : 0;

        dragDistance = 0;
        samples.length = 0;
        stopMomentum();
    });

    getMainElement().addEventListener('scroll', (event) => {
        event.preventDefault();
    });

    el.addEventListener('touchmove', (event) => {
        const touch = Array.from(event.touches).find(t => t.identifier === primaryTouchId);
        if (!touch) return;

        event.preventDefault();

        const currentTouchY = touch.clientY;
        const totalDeltaY = startTouchY - currentTouchY;
        dragDistance = Math.abs(totalDeltaY);

        if (dragDistance > TAP_THRESHOLD_PX) {
            passScrollState.wasDrag = true;
        }

        const target = getMainElement();
        if (target) {
            target.scrollTop = startScrollY + totalDeltaY;
        }

        sampleTouch(currentTouchY, performance.now());
    });

    function resetDragState() {
        setTimeout(() => {
            passScrollState.wasDrag = false;
        }, 50);
        primaryTouchId = null;
    }

    el.addEventListener('touchend', (event) => {
        const touchEnded = Array.from(event.changedTouches).some(t => t.identifier === primaryTouchId);
        if (!touchEnded) return;

        // event.preventDefault();

        const rawVelocity = calculateVelocity();
        velocity = Math.max(-MOMENTUM_MAX_VELOCITY, Math.min(MOMENTUM_MAX_VELOCITY, rawVelocity));
        samples.length = 0;

        if (Math.abs(velocity) >= MOMENTUM_MIN_VELOCITY) {
            momentumStart = performance.now();
            lastFrameTime = 0;
            momentumFrameId = requestAnimationFrame(runMomentumFrame);
        }

        resetDragState();
    });

    el.addEventListener('touchcancel', (event) => {
        const touchCancelled = Array.from(event.changedTouches).some(t => t.identifier === primaryTouchId);
        if (!touchCancelled) return;

        // event.preventDefault();

        stopMomentum();
        resetDragState();
    });
}

document.querySelectorAll(PASS_SCROLL_SELECTOR).forEach(passScrollToMain);
