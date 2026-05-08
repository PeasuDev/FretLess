const animations = new Map();

export function start(canvas, options) {
    if (!canvas) {
        return;
    }

    stop(canvas);

    const context = canvas.getContext('2d', { alpha: true });
    const state = {
        context,
        frameId: 0,
        startedAt: performance.now(),
        reducedMotion: window.matchMedia('(prefers-reduced-motion: reduce)').matches,
        options: normalizeOptions(options)
    };

    const renderFrame = (timestamp) => {
        resizeCanvas(canvas, context);
        draw(canvas, state, timestamp);

        if (!state.reducedMotion) {
            state.frameId = requestAnimationFrame(renderFrame);
        }
    };

    animations.set(canvas, state);
    renderFrame(state.startedAt);
}

export function update(canvas, options) {
    const state = animations.get(canvas);
    if (!state) {
        start(canvas, options);
        return;
    }

    state.options = normalizeOptions(options);
}

export function stop(canvas) {
    const state = animations.get(canvas);
    if (!state) {
        return;
    }

    cancelAnimationFrame(state.frameId);
    animations.delete(canvas);
}

function normalizeOptions(options) {
    return {
        accentColor: options?.accentColor ?? '#00e5ff',
        secondaryColor: options?.secondaryColor ?? '#ffd700',
        backgroundColor: options?.backgroundColor ?? 'rgba(0, 0, 0, 0)',
        particleCount: Number(options?.particleCount ?? 72),
        speed: Number(options?.speed ?? 1),
        radiusScale: Number(options?.radiusScale ?? 1)
    };
}

function resizeCanvas(canvas, context) {
    const rect = canvas.getBoundingClientRect();
    const devicePixelRatio = Math.max(1, window.devicePixelRatio || 1);
    const width = Math.max(1, Math.round(rect.width * devicePixelRatio));
    const height = Math.max(1, Math.round(rect.height * devicePixelRatio));

    if (canvas.width !== width || canvas.height !== height) {
        canvas.width = width;
        canvas.height = height;
    }

    context.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
}

function draw(canvas, state, timestamp) {
    const { context, options } = state;
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    const centerX = width / 2;
    const centerY = height / 2;
    const elapsed = (timestamp - state.startedAt) / 1000;
    const time = state.reducedMotion ? 0 : elapsed * options.speed;
    const minDimension = Math.min(width, height);

    context.clearRect(0, 0, width, height);

    if (options.backgroundColor !== 'rgba(0, 0, 0, 0)') {
        context.fillStyle = options.backgroundColor;
        context.fillRect(0, 0, width, height);
    }

    drawOrbitalField(context, options, centerX, centerY, minDimension, time);
    drawPulseRings(context, options, centerX, centerY, minDimension, time);
}

function drawOrbitalField(context, options, centerX, centerY, minDimension, time) {
    const count = Math.max(12, Math.min(180, options.particleCount));
    const maxRadius = minDimension * 0.42 * options.radiusScale;

    context.save();
    context.globalCompositeOperation = 'lighter';

    for (let i = 0; i < count; i++) {
        const progress = i / count;
        const angle = progress * Math.PI * 14 + time * (0.45 + progress * 0.85);
        const radius = maxRadius * Math.sqrt(progress);
        const wave = Math.sin(time * 1.8 + progress * Math.PI * 8) * minDimension * 0.025;
        const x = centerX + Math.cos(angle) * (radius + wave);
        const y = centerY + Math.sin(angle) * (radius + wave);
        const particleRadius = 1.2 + (1 - progress) * 2.8;

        context.beginPath();
        context.arc(x, y, particleRadius, 0, Math.PI * 2);
        context.fillStyle = progress % 0.18 < 0.09 ? options.accentColor : options.secondaryColor;
        context.globalAlpha = 0.18 + (1 - progress) * 0.58;
        context.fill();
    }

    context.restore();
}

function drawPulseRings(context, options, centerX, centerY, minDimension, time) {
    const maxRadius = minDimension * 0.44 * options.radiusScale;

    context.save();
    context.lineWidth = 1.4;

    for (let i = 0; i < 4; i++) {
        const progress = ((time * 0.18) + i * 0.25) % 1;
        const radius = 24 + progress * maxRadius;
        const alpha = (1 - progress) * 0.28;

        context.beginPath();
        context.arc(centerX, centerY, radius, 0, Math.PI * 2);
        context.strokeStyle = i % 2 === 0 ? options.accentColor : options.secondaryColor;
        context.globalAlpha = alpha;
        context.stroke();
    }

    context.restore();
}
