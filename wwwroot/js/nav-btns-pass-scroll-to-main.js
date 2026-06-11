const passScrollSelector = '.pass-scroll-to-main';
const TAP_THRESHOLD_PX = 8;

const MOMENTUM_FRICTION = 0.94;
const MOMENTUM_MAX_VELOCITY = 30;
const MOMENTUM_MIN_VELOCITY = 0.5;
const MOMENTUM_MAX_DURATION_MS = 1500;
const MOMENTUM_SAMPLE_WINDOW_MS = 60;

const passScrollState = {wasDrag: false};

function passScrollToMain(el) {
    let pendingDelta = 0;
    let frameScheduled = false;
    let previousTouchY = 0;
    let dragDistance = 0;
    let velocity = 0;
    let momentumStart = 0;
    const samples = [];

    function flushDelta() {
        mainElement.scrollBy(0, pendingDelta);
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
        samples.push({y, t});
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
        return ((first.y - last.y) / dt) * 16;
    }

    function stopMomentum() {
        velocity = 0;
    }

    function runMomentumFrame() {
        if (Math.abs(velocity) < MOMENTUM_MIN_VELOCITY) return;
        if (performance.now() - momentumStart > MOMENTUM_MAX_DURATION_MS) return;

        enqueueDelta(velocity);
        velocity *= MOMENTUM_FRICTION;
        requestAnimationFrame(runMomentumFrame);
    }

    el.addEventListener('wheel', (event) => {
        enqueueDelta(event.deltaY);
    }, {passive: true});

    el.addEventListener('touchstart', (event) => {
        previousTouchY = event.touches[0].clientY;
        dragDistance = 0;
        samples.length = 0;
        stopMomentum();
    }, {passive: true});

    el.addEventListener('touchmove', (event) => {
        const currentTouchY = event.touches[0].clientY;
        const delta = previousTouchY - currentTouchY;

        dragDistance += Math.abs(delta);
        if (dragDistance > TAP_THRESHOLD_PX) {
            passScrollState.wasDrag = true;
        }

        enqueueDelta(delta);
        sampleTouch(currentTouchY, performance.now());

        previousTouchY = currentTouchY;
    }, {passive: true});

    function endTouch() {
        velocity = Math.max(-MOMENTUM_MAX_VELOCITY,
            Math.min(MOMENTUM_MAX_VELOCITY, currentVelocity()));
        samples.length = 0;
        if (Math.abs(velocity) >= MOMENTUM_MIN_VELOCITY) {
            momentumStart = performance.now();
            requestAnimationFrame(runMomentumFrame);
        }
        setTimeout(() => {
            passScrollState.wasDrag = false;
        }, 0);
    }

    el.addEventListener('touchend', endTouch, {passive: true});
    el.addEventListener('touchcancel', () => {
        stopMomentum();
        setTimeout(() => {
            passScrollState.wasDrag = false;
        }, 0);
    }, {passive: true});
}

document.querySelectorAll(passScrollSelector).forEach(passScrollToMain);
