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

(function initPassScrollToMain() {
    const targets = new WeakSet();
    let mainElement = null;

    let startTouchY = 0;
    let startScrollY = 0;
    let dragDistance = 0;

    let velocity = 0;
    let momentumStart = 0;
    let lastFrameTime = 0;
    let momentumFrameId = null;
    let primaryTouchId = null;
    let wasDragResetTimer = null;
    let pendingScrollTop = null;
    let touchScrollRafId = null;

    const samples = [];

    function getMainElement() {
        if (!mainElement || !mainElement.isConnected) {
            mainElement = document.querySelector('main') || document.scrollingElement || null;
        }
        return mainElement;
    }

    function isPassScrollTarget(node) {
        if (!(node instanceof Element)) {
            return false;
        }

        let element = node;
        while (element) {
            if (targets.has(element)) {
                return true;
            }
            element = element.parentElement;
        }

        return false;
    }

    function registerTarget(element) {
        if (!(element instanceof Element) || targets.has(element)) {
            return;
        }

        targets.add(element);
        element.style.touchAction = 'none';
    }

    function sampleTouch(y, t) {
        samples.push({y, t});
        while (samples.length > 0 && t - samples[0].t > MOMENTUM_SAMPLE_WINDOW_MS) {
            samples.shift();
        }
    }

    function calculateVelocity() {
        if (samples.length < 2) {
            return 0;
        }

        const first = samples[0];
        const last = samples[samples.length - 1];
        const dt = last.t - first.t;
        if (dt <= 0) {
            return 0;
        }

        return (first.y - last.y) / dt;
    }

    function stopMomentum() {
        velocity = 0;
        if (momentumFrameId !== null) {
            cancelAnimationFrame(momentumFrameId);
            momentumFrameId = null;
        }
    }

    function cancelPendingTouchScroll() {
        pendingScrollTop = null;
        if (touchScrollRafId !== null) {
            cancelAnimationFrame(touchScrollRafId);
            touchScrollRafId = null;
        }
    }

    function applyPendingScroll() {
        touchScrollRafId = null;
        const main = getMainElement();
        if (main && pendingScrollTop !== null) {
            main.scrollTop = pendingScrollTop;
        }
    }

    function scheduleScrollTop(value) {
        pendingScrollTop = value;
        if (touchScrollRafId === null) {
            touchScrollRafId = requestAnimationFrame(applyPendingScroll);
        }
    }

    function runMomentumFrame(now) {
        momentumFrameId = null;

        if (Math.abs(velocity) < MOMENTUM_MIN_VELOCITY) {
            return;
        }
        if (now - momentumStart > MOMENTUM_MAX_DURATION_MS) {
            return;
        }

        const dt = lastFrameTime ? now - lastFrameTime : 16.6;
        lastFrameTime = now;

        const main = getMainElement();
        if (!main) {
            stopMomentum();
            return;
        }

        main.scrollBy({top: velocity * dt, behavior: 'auto'});
        velocity *= Math.pow(MOMENTUM_FRICTION, dt / 16.6);

        if (Math.abs(velocity) >= MOMENTUM_MIN_VELOCITY) {
            momentumFrameId = requestAnimationFrame(runMomentumFrame);
        }
    }

    function resetDragState() {
        if (wasDragResetTimer !== null) {
            clearTimeout(wasDragResetTimer);
        }

        wasDragResetTimer = setTimeout(() => {
            passScrollState.wasDrag = false;
            wasDragResetTimer = null;
        }, 50);

        primaryTouchId = null;
    }

    function onWheel(event) {
        if (!isPassScrollTarget(event.target)) {
            return;
        }

        event.preventDefault();

        const main = getMainElement();
        if (main) {
            main.scrollBy({top: event.deltaY, behavior: 'auto'});
        }
    }

    function onTouchStart(event) {
        if (!isPassScrollTarget(event.target)) {
            return;
        }

        const touch = event.changedTouches[0];
        if (!touch) {
            return;
        }

        primaryTouchId = touch.identifier;
        startTouchY = touch.clientY;

        const main = getMainElement();
        startScrollY = main ? main.scrollTop : 0;

        dragDistance = 0;
        samples.length = 0;
        stopMomentum();
        cancelPendingTouchScroll();
    }

    function onTouchMove(event) {
        if (primaryTouchId === null) {
            return;
        }

        const touch = Array.from(event.touches).find(t => t.identifier === primaryTouchId);
        if (!touch) {
            return;
        }

        event.preventDefault();

        const currentTouchY = touch.clientY;
        const totalDeltaY = startTouchY - currentTouchY;
        dragDistance = Math.abs(totalDeltaY);

        if (dragDistance > TAP_THRESHOLD_PX) {
            passScrollState.wasDrag = true;
        }

        scheduleScrollTop(startScrollY + totalDeltaY);
        sampleTouch(currentTouchY, performance.now());
    }

    function finishTouch(event) {
        if (primaryTouchId === null) {
            return false;
        }

        const touch = Array.from(event.changedTouches).find(t => t.identifier === primaryTouchId);
        if (!touch) {
            return false;
        }

        cancelPendingTouchScroll();

        const totalDeltaY = startTouchY - touch.clientY;
        const main = getMainElement();
        if (main) {
            main.scrollTop = startScrollY + totalDeltaY;
        }

        const rawVelocity = calculateVelocity();
        velocity = Math.max(-MOMENTUM_MAX_VELOCITY, Math.min(MOMENTUM_MAX_VELOCITY, rawVelocity));
        samples.length = 0;

        if (Math.abs(velocity) >= MOMENTUM_MIN_VELOCITY) {
            momentumStart = performance.now();
            lastFrameTime = 0;
            momentumFrameId = requestAnimationFrame(runMomentumFrame);
        }

        resetDragState();
        return true;
    }

    function onTouchEnd(event) {
        finishTouch(event);
    }

    function onTouchCancel(event) {
        if (primaryTouchId === null) {
            return;
        }

        const touchCancelled = Array.from(event.changedTouches)
            .some(t => t.identifier === primaryTouchId);
        if (!touchCancelled) {
            return;
        }

        stopMomentum();
        cancelPendingTouchScroll();
        resetDragState();
    }

    document.addEventListener('wheel', onWheel, {passive: false});
    document.addEventListener('touchstart', onTouchStart, {passive: true});
    document.addEventListener('touchmove', onTouchMove, {passive: false});
    document.addEventListener('touchend', onTouchEnd, {passive: true});
    document.addEventListener('touchcancel', onTouchCancel, {passive: true});

    document.querySelectorAll(PASS_SCROLL_SELECTOR).forEach(registerTarget);
})();
