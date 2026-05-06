// fretlessAudio — lightweight Web Audio API synth (no external dependencies)
window.fretlessAudio = (() => {
    let ctx = null;
    let masterGain = null;
    let masterCompressor = null;
    let masterLowpass = null;
    let userVolume = 0.72;
    let isMuted = false;

    function getCtx() {
        if (!ctx) ctx = new (window.AudioContext || window.webkitAudioContext)();
        if (ctx.state === 'suspended') ctx.resume();
        ensureMasterChain(ctx);
        return ctx;
    }

    function ensureMasterChain(ac) {
        if (masterGain && masterCompressor && masterLowpass) {
            return;
        }

        masterGain = ac.createGain();
        masterCompressor = ac.createDynamicsCompressor();
        masterLowpass = ac.createBiquadFilter();

        masterGain.gain.value = isMuted ? 0 : userVolume;

        masterLowpass.type = 'lowpass';
        masterLowpass.frequency.value = 6800;
        masterLowpass.Q.value = 0.4;

        masterCompressor.threshold.value = -14;
        masterCompressor.knee.value = 18;
        masterCompressor.ratio.value = 1.6;
        masterCompressor.attack.value = 0.02;
        masterCompressor.release.value = 0.12;

        masterGain.connect(masterLowpass);
        masterLowpass.connect(masterCompressor);
        masterCompressor.connect(ac.destination);
    }

    // Convert note name like "C4", "F#4", "Bb3", or "D#10" to frequency
    function noteToFreq(note) {
        const names = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
        const enharmonic = { 'Db': 'C#', 'Eb': 'D#', 'Fb': 'E', 'Gb': 'F#', 'Ab': 'G#', 'Bb': 'A#', 'Cb': 'B' };
        const m = note.match(/^([A-G][b#]?)(-?\d+)$/);
        if (!m) return 440;
        const name = enharmonic[m[1]] ?? m[1];
        const octave = parseInt(m[2], 10);
        const semitone = names.indexOf(name);
        const midi = (octave + 1) * 12 + semitone;
        return 440 * Math.pow(2, (midi - 69) / 12);
    }

    function createVoiceBus(startTime, duration, level = 1.0) {
        const ac = getCtx();
        const voiceBus = ac.createGain();
        const releasePad = 0.06;
        voiceBus.gain.setValueAtTime(level, startTime);
        voiceBus.gain.setValueAtTime(level, startTime + duration);
        voiceBus.gain.exponentialRampToValueAtTime(0.0001, startTime + duration + releasePad);
        voiceBus.connect(masterGain);
        return voiceBus;
    }

    function playOscillator(freq, startTime, duration, gainValue = 0.18, type = 'sine', destination = null) {
        const ac = getCtx();
        const osc = ac.createOscillator();
        const gain = ac.createGain();

        osc.type = type;
        osc.frequency.setValueAtTime(freq, startTime);

        const attack = 0.012;
        const decay = 0.12;
        const sustainLevel = gainValue * 0.52;
        const releaseStart = Math.max(startTime + attack + decay, startTime + duration * 0.72);

        gain.gain.setValueAtTime(0, startTime);
        gain.gain.linearRampToValueAtTime(gainValue, startTime + attack);
        gain.gain.exponentialRampToValueAtTime(Math.max(0.0001, sustainLevel), startTime + attack + decay);
        gain.gain.setValueAtTime(Math.max(0.0001, sustainLevel), releaseStart);
        gain.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);

        osc.connect(gain);
        gain.connect(destination ?? masterGain);

        osc.start(startTime);
        osc.stop(startTime + duration + 0.05);
    }

    return {
        playNote(noteName) {
            const ac = getCtx();
            const now = ac.currentTime;
            const voiceBus = createVoiceBus(now, 1.45, 1.0);
            playOscillator(noteToFreq(noteName), now, 1.4, 0.14, 'triangle', voiceBus);
        },
        playFrequency(freq) {
            const ac = getCtx();
            const now = ac.currentTime;
            const voiceBus = createVoiceBus(now, 1.45, 1.0);
            const safeFreq = Number.isFinite(freq) && freq > 0 ? freq : 440;
            playOscillator(safeFreq, now, 1.4, 0.14, 'triangle', voiceBus);
        },
        setMasterVolume(level) {
            const ac = getCtx();
            const n = Number(level);
            userVolume = Number.isFinite(n) ? Math.min(1, Math.max(0, n)) : 0.72;
            if (!isMuted) {
                masterGain.gain.setTargetAtTime(userVolume, ac.currentTime, 0.01);
            }
        },
        setMuted(muted) {
            const ac = getCtx();
            isMuted = !!muted;
            masterGain.gain.setTargetAtTime(isMuted ? 0 : userVolume, ac.currentTime, 0.01);
        },
        playOvertones(noteName, maxFreq = 20000) {
            const ac = getCtx();
            const fundamental = noteToFreq(noteName);
            if (!Number.isFinite(fundamental) || fundamental <= 0) {
                return;
            }

            const now = ac.currentTime;
            const voiceBus = createVoiceBus(now, 1.9, 1.0);
            const max = Number.isFinite(maxFreq) && maxFreq > 0 ? maxFreq : 20000;
            const startHarmonic = 1;
            const tonalCeiling = Math.min(max, 9000);
            const harmonicCount = Math.min(24, Math.floor(tonalCeiling / fundamental));

            for (let harmonic = startHarmonic; harmonic <= harmonicCount; harmonic++) {
                const freq = fundamental * harmonic;
                const spectralRolloff = Math.exp(-Math.pow(freq / 5200, 1.35));
                const harmonicWeight = 1 / Math.pow(harmonic, 1.32);
                const gain = 0.26 * harmonicWeight * spectralRolloff;
                const shapedGain = Math.max(0.0001, gain);
                const wave = harmonic === 1 ? 'triangle' : 'sine';
                const offset = harmonic * 0.0025;
                playOscillator(freq, now + offset, 1.8, shapedGain, wave, voiceBus);
            }
        },
        playChord(notes) {
            const ac = getCtx();
            const now = ac.currentTime;
            const voiceBus = createVoiceBus(now, 2.1, 1.0);
            // Slight strum effect — each note delayed by 25ms
            notes.forEach((n, i) => {
                playOscillator(noteToFreq(n), now + i * 0.028, 2.0, 0.11, 'triangle', voiceBus);
            });
        }
    };
})();

// ── Fretboard resize drag helper ──────────────────────────
window.fretlessResizeStart = function (dotNetRef) {
    const onMove = (e) => {
        const clientY = e.touches ? e.touches[0].clientY : e.clientY;
        dotNetRef.invokeMethodAsync('OnResizeMove', clientY);
    };
    const onUp = () => {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup', onUp);
        document.removeEventListener('touchmove', onMove);
        document.removeEventListener('touchend', onUp);
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
    };
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
    document.addEventListener('touchmove', onMove, { passive: true });
    document.addEventListener('touchend', onUp);
    document.body.style.cursor = 'ns-resize';
    document.body.style.userSelect = 'none';
};
