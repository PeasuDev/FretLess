// fretlessAudio — lightweight Web Audio API synth (no external dependencies)
window.fretlessAudio = (() => {
    let ctx = null;

    function getCtx() {
        if (!ctx) ctx = new (window.AudioContext || window.webkitAudioContext)();
        if (ctx.state === 'suspended') ctx.resume();
        return ctx;
    }

    // Convert note name like "C4", "F#4", "Bb3" to frequency
    function noteToFreq(note) {
        const names = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
        const enharmonic = { 'Db': 'C#', 'Eb': 'D#', 'Fb': 'E', 'Gb': 'F#', 'Ab': 'G#', 'Bb': 'A#', 'Cb': 'B' };
        const m = note.match(/^([A-G][b#]?)(\d)$/);
        if (!m) return 440;
        const name = enharmonic[m[1]] ?? m[1];
        const octave = parseInt(m[2]);
        const semitone = names.indexOf(name);
        const midi = (octave + 1) * 12 + semitone;
        return 440 * Math.pow(2, (midi - 69) / 12);
    }

    function playOscillator(freq, startTime, duration, gainValue = 0.18) {
        const ac = getCtx();
        const osc = ac.createOscillator();
        const gain = ac.createGain();
        const compressor = ac.createDynamicsCompressor();

        osc.type = 'triangle';
        osc.frequency.setValueAtTime(freq, startTime);

        gain.gain.setValueAtTime(0, startTime);
        gain.gain.linearRampToValueAtTime(gainValue, startTime + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.001, startTime + duration);

        osc.connect(gain);
        gain.connect(compressor);
        compressor.connect(ac.destination);

        osc.start(startTime);
        osc.stop(startTime + duration + 0.05);
    }

    return {
        playNote(noteName) {
            const ac = getCtx();
            playOscillator(noteToFreq(noteName), ac.currentTime, 1.2);
        },
        playChord(notes) {
            const ac = getCtx();
            const now = ac.currentTime;
            // Slight strum effect — each note delayed by 25ms
            notes.forEach((n, i) => {
                playOscillator(noteToFreq(n), now + i * 0.025, 2.0, 0.14);
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
