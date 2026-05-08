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
        },
        playScale(notes) {
            if (!Array.isArray(notes) || notes.length === 0) {
                return;
            }

            const ac = getCtx();
            const stepDuration = 0.32;
            const noteDuration = 0.45;
            const now = ac.currentTime;
            const voiceBus = createVoiceBus(now, notes.length * stepDuration + noteDuration, 1.0);

            notes.forEach((note, index) => {
                playOscillator(noteToFreq(note), now + index * stepDuration, noteDuration, 0.12, 'triangle', voiceBus);
            });
        }
    };
})();

// ── Analog audio input pitch detector ──────────────────────
window.fretlessAudioInput = (() => {
    let _dotNetRef = null;
    let _ctx = null;
    let _stream = null;
    let _source = null;
    let _analyser = null;
    let _buffer = null;
    let _rafId = null;
    let _activeMidi = null;
    let _candidateMidi = null;
    let _candidateFrames = 0;
    let _quietFrames = 0;
    let _waveformCanvas = null;
    let _waveformCtx = null;

    const MIN_FREQUENCY = 55;
    const MAX_FREQUENCY = 1760;
    const MIN_RMS = 0.018;
    const NOTE_ON_STABLE_FRAMES = 3;
    const NOTE_CHANGE_STABLE_FRAMES = 4;
    const NOTE_OFF_QUIET_FRAMES = 8;

    function calculateRms(buffer) {
        let rms = 0;
        for (let i = 0; i < buffer.length; i++) {
            rms += buffer[i] * buffer[i];
        }

        return Math.sqrt(rms / buffer.length);
    }

    function frequencyToMidi(frequency) {
        return Math.round(69 + 12 * Math.log2(frequency / 440));
    }

    function drawWaveform(buffer) {
        if (!_waveformCanvas || !_waveformCtx || !buffer) {
            return;
        }

        const width = _waveformCanvas.clientWidth || _waveformCanvas.width;
        const height = _waveformCanvas.clientHeight || _waveformCanvas.height;
        const deviceRatio = window.devicePixelRatio || 1;
        const pixelWidth = Math.max(1, Math.floor(width * deviceRatio));
        const pixelHeight = Math.max(1, Math.floor(height * deviceRatio));

        if (_waveformCanvas.width !== pixelWidth || _waveformCanvas.height !== pixelHeight) {
            _waveformCanvas.width = pixelWidth;
            _waveformCanvas.height = pixelHeight;
        }

        _waveformCtx.setTransform(deviceRatio, 0, 0, deviceRatio, 0, 0);
        _waveformCtx.clearRect(0, 0, width, height);

        const gradient = _waveformCtx.createLinearGradient(0, 0, width, 0);
        gradient.addColorStop(0, 'rgba(0, 229, 255, 0.35)');
        gradient.addColorStop(0.5, 'rgba(0, 229, 255, 1)');
        gradient.addColorStop(1, 'rgba(255, 215, 0, 0.7)');

        _waveformCtx.lineWidth = 2;
        _waveformCtx.strokeStyle = gradient;
        _waveformCtx.beginPath();

        const step = Math.max(1, Math.floor(buffer.length / width));
        for (let x = 0; x < width; x++) {
            const sample = buffer[Math.min(buffer.length - 1, x * step)] || 0;
            const y = height * 0.5 + Math.max(-1, Math.min(1, sample)) * height * 0.42;
            if (x === 0) {
                _waveformCtx.moveTo(x, y);
            } else {
                _waveformCtx.lineTo(x, y);
            }
        }

        _waveformCtx.stroke();
    }

    function estimatePitch(buffer, sampleRate) {
        const rms = calculateRms(buffer);
        if (rms < MIN_RMS) {
            return null;
        }

        const minLag = Math.floor(sampleRate / MAX_FREQUENCY);
        const maxLag = Math.min(Math.floor(sampleRate / MIN_FREQUENCY), Math.floor(buffer.length / 2));
        let bestLag = -1;
        let bestCorrelation = 0;

        for (let lag = minLag; lag <= maxLag; lag++) {
            let correlation = 0;
            for (let i = 0; i < buffer.length - lag; i++) {
                correlation += buffer[i] * buffer[i + lag];
            }

            correlation /= buffer.length - lag;
            if (correlation > bestCorrelation) {
                bestCorrelation = correlation;
                bestLag = lag;
            }
        }

        if (bestLag <= 0 || bestCorrelation < 0.0025) {
            return null;
        }

        const frequency = sampleRate / bestLag;
        if (!Number.isFinite(frequency) || frequency < MIN_FREQUENCY || frequency > MAX_FREQUENCY) {
            return null;
        }

        return { frequency, rms };
    }

    function sendNoteOn(midi) {
        _activeMidi = midi;
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnAudioInputNoteOn', midi);
        }
    }

    function sendNoteOff() {
        if (_activeMidi === null) {
            return;
        }

        const midi = _activeMidi;
        _activeMidi = null;
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnAudioInputNoteOff', midi);
        }
    }

    function processFrame() {
        if (!_analyser || !_buffer) {
            _rafId = null;
            return;
        }

        _analyser.getFloatTimeDomainData(_buffer);
        drawWaveform(_buffer);
        const rms = calculateRms(_buffer);
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnAudioInputLevelDetected', rms);
        }

        const pitch = estimatePitch(_buffer, _ctx.sampleRate);

        if (!pitch) {
            _candidateMidi = null;
            _candidateFrames = 0;
            _quietFrames++;
            if (_quietFrames >= NOTE_OFF_QUIET_FRAMES) {
                sendNoteOff();
            }

            _rafId = requestAnimationFrame(processFrame);
            return;
        }

        _quietFrames = 0;
        const midi = Math.min(127, Math.max(0, frequencyToMidi(pitch.frequency)));
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnAudioInputPitchDetected', midi, pitch.frequency, pitch.rms);
        }

        if (_candidateMidi === midi) {
            _candidateFrames++;
        } else {
            _candidateMidi = midi;
            _candidateFrames = 1;
        }

        const stableFrames = _activeMidi === null ? NOTE_ON_STABLE_FRAMES : NOTE_CHANGE_STABLE_FRAMES;
        if (_candidateFrames >= stableFrames && _activeMidi !== midi) {
            sendNoteOff();
            sendNoteOn(midi);
        }

        _rafId = requestAnimationFrame(processFrame);
    }

    async function stopInternal() {
        if (_rafId !== null) {
            cancelAnimationFrame(_rafId);
            _rafId = null;
        }

        sendNoteOff();
        _candidateMidi = null;
        _candidateFrames = 0;
        _quietFrames = 0;

        if (_source) {
            _source.disconnect();
            _source = null;
        }

        _analyser = null;
        _buffer = null;

        if (_stream) {
            _stream.getTracks().forEach(track => track.stop());
            _stream = null;
        }

        if (_ctx) {
            await _ctx.close();
            _ctx = null;
        }
    }

    function baseAudioConstraints() {
        return {
            echoCancellation: false,
            noiseSuppression: false,
            autoGainControl: false
        };
    }

    function isAudioInterfaceMethod(recordingMethod) {
        return String(recordingMethod ?? '').toLowerCase().includes('interface');
    }

    async function findAudioInterfaceInput() {
        if (!navigator.mediaDevices?.enumerateDevices) {
            return null;
        }

        try {
            const devices = await navigator.mediaDevices.enumerateDevices();
            return devices.find(device =>
                device.kind === 'audioinput'
                && /interface|usb|asio|focusrite|scarlett|presonus|behringer|steinberg|motu|apollo|line/i.test(device.label));
        } catch (err) {
            console.warn('Audio input device selection unavailable:', err);
            return null;
        }
    }

    return {
        async start(dotNetRef, recordingMethod = 'Microphone') {
            _dotNetRef = dotNetRef;
            if (!navigator.mediaDevices?.getUserMedia) {
                console.warn('Audio input is not supported in this browser.');
                return false;
            }

            await stopInternal();

            try {
                const audioConstraints = baseAudioConstraints();
                _stream = await navigator.mediaDevices.getUserMedia({
                    audio: audioConstraints
                });

                if (isAudioInterfaceMethod(recordingMethod)) {
                    const interfaceInput = await findAudioInterfaceInput();
                    const currentTrack = _stream.getAudioTracks()[0];
                    const currentDeviceId = currentTrack?.getSettings?.().deviceId;

                    if (interfaceInput && interfaceInput.deviceId !== currentDeviceId) {
                        _stream.getTracks().forEach(track => track.stop());
                        _stream = await navigator.mediaDevices.getUserMedia({
                            audio: { ...baseAudioConstraints(), deviceId: { exact: interfaceInput.deviceId } }
                        });
                    }
                }

                _ctx = new (window.AudioContext || window.webkitAudioContext)();
                _source = _ctx.createMediaStreamSource(_stream);
                _analyser = _ctx.createAnalyser();
                _analyser.fftSize = 4096;
                _buffer = new Float32Array(_analyser.fftSize);
                _source.connect(_analyser);
                _rafId = requestAnimationFrame(processFrame);
                return true;
            } catch (err) {
                console.warn('Audio input unavailable:', err);
                await stopInternal();
                return false;
            }
        },
        async stop() {
            await stopInternal();
        },
        attachWaveformCanvas(canvas) {
            _waveformCanvas = canvas;
            _waveformCtx = canvas?.getContext?.('2d') ?? null;
        },
        async dispose() {
            await stopInternal();
            _waveformCanvas = null;
            _waveformCtx = null;
            _dotNetRef = null;
        }
    };
})();

// ── MIDI input handler ────────────────────────────────────
window.fretlessMidi = (() => {
    let _dotNetRef = null;
    let _inputs = new Map();

    function midiToNoteName(midi) {
        const names = ['C', 'Db', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'];
        const octave = Math.floor(midi / 12) - 1;
        return `${names[midi % 12]}${octave}`;
    }

    function playNoteAfterHighlight(note) {
        const play = () => window.fretlessAudio.playNote(midiToNoteName(note));

        if (!_dotNetRef) {
            play();
            return;
        }

        _dotNetRef.invokeMethodAsync('OnMidiNoteOn', note)
            .finally(() => requestAnimationFrame(play));
    }

    function onMidiMessage(e) {
        const [status, note, velocity] = e.data;
        const type = status & 0xf0;
        const isNoteOn  = type === 0x90 && velocity > 0;
        const isNoteOff = type === 0x80 || (type === 0x90 && velocity === 0);
        if (isNoteOn) {
            playNoteAfterHighlight(note);
        } else if (isNoteOff) {
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('OnMidiNoteOff', note);
        }
    }

    function attachInputs(access) {
        access.inputs.forEach(input => {
            input.addEventListener('midimessage', onMidiMessage);
            _inputs.set(input.id, input);
        });
        access.onstatechange = (e) => {
            if (e.port.type !== 'input') return;
            if (e.port.state === 'connected') {
                e.port.addEventListener('midimessage', onMidiMessage);
                _inputs.set(e.port.id, e.port);
            } else {
                e.port.removeEventListener('midimessage', onMidiMessage);
                _inputs.delete(e.port.id);
            }
        };
    }

    return {
        async init(dotNetRef) {
            _dotNetRef = dotNetRef;
            if (!navigator.requestMIDIAccess) {
                console.warn('Web MIDI API not supported in this browser.');
                return;
            }
            try {
                const access = await navigator.requestMIDIAccess({ sysex: false });
                attachInputs(access);
            } catch (err) {
                console.warn('MIDI access denied:', err);
            }
        },
        dispose() {
            _inputs.forEach(input => input.removeEventListener('midimessage', onMidiMessage));
            _inputs.clear();
            _dotNetRef = null;
        }
    };
})();

// ── Spiral SVG zoom/pan animation helper ──────────────────
// Drives viewBox changes via rAF so Blazor re-render is NOT triggered each frame.
window.fretlessSpiralZoom = (() => {
    let _rafId = null;
    let _svgEl = null;
    let _zoom = 1.0;
    let _targetZoom = 1.0;
    let _panX = 0.0;
    let _panY = 0.0;
    let _dotNetRef = null;

    const EPSILON = 0.005;
    const SMOOTHING = 0.30;
    const HALF_VB = 210.0;

    function viewBox() {
        const half = HALF_VB / _zoom;
        return `${_panX - half} ${_panY - half} ${half * 2} ${half * 2}`;
    }

    function tick() {
        if (!_svgEl) { _rafId = null; return; }

        const diff = _targetZoom - _zoom;
        if (Math.abs(diff) <= EPSILON) {
            _zoom = _targetZoom;
            _svgEl.setAttribute('viewBox', viewBox());
            _rafId = null;
            // Notify Blazor of final settled zoom value (single re-render)
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('OnJsZoomSettled', _zoom, _panX, _panY);
            return;
        }

        _zoom += diff * SMOOTHING;
        _svgEl.setAttribute('viewBox', viewBox());
        _rafId = requestAnimationFrame(tick);
    }

    return {
        init(svgEl, dotNetRef, zoom, panX, panY) {
            _svgEl = svgEl;
            _dotNetRef = dotNetRef;
            _zoom = zoom;
            _targetZoom = zoom;
            _panX = panX;
            _panY = panY;
            if (_svgEl) _svgEl.setAttribute('viewBox', viewBox());
        },
        setTarget(targetZoom, panX, panY) {
            _targetZoom = targetZoom;
            _panX = panX;
            _panY = panY;
            if (!_rafId) _rafId = requestAnimationFrame(tick);
        },
        setPan(panX, panY) {
            _panX = panX;
            _panY = panY;
            if (_svgEl) _svgEl.setAttribute('viewBox', viewBox());
        },
        dispose() {
            if (_rafId) { cancelAnimationFrame(_rafId); _rafId = null; }
            _svgEl = null;
            _dotNetRef = null;
        }
    };
})();

// ── Spiral SVG rotation animation helper ──────────────────
// Drives rotation changes via rAF so Blazor re-render is NOT triggered each frame.
window.fretlessSpiralRotation = (() => {
    let _rafId = null;
    let _svgEl = null;
    let _rotationLayer = null;
    let _uprightLabels = [];
    let _currentRotation = 0.0;
    let _targetRotation = 0.0;
    let _dotNetRef = null;

    const EPSILON = 0.05;
    const DURATION_MS = 280;

    function normalizeDegrees(degrees) {
        let normalized = degrees % 360;
        if (normalized <= -180) normalized += 360;
        else if (normalized > 180) normalized -= 360;
        return normalized;
    }

    function shortestAngularDelta(fromDegrees, toDegrees) {
        return ((toDegrees - fromDegrees + 540) % 360) - 180;
    }

    function smoothStep(progress) {
        const t = Math.min(1, Math.max(0, progress));
        return t * t * (3 - 2 * t);
    }

    function refreshElements() {
        if (!_svgEl) {
            _rotationLayer = null;
            _uprightLabels = [];
            return;
        }

        _rotationLayer = _svgEl.querySelector('.spiral-rotation-layer');
        _uprightLabels = Array.from(_svgEl.querySelectorAll('.spiral-upright-label'));
    }

    function applyRotation(rotationDegrees) {
        if (!_rotationLayer || !_rotationLayer.isConnected) {
            refreshElements();
        }

        if (!_rotationLayer) {
            return;
        }

        _rotationLayer.setAttribute('transform', `rotate(${rotationDegrees.toFixed(3)})`);

        const counterRotation = -rotationDegrees;
        for (const label of _uprightLabels) {
            const x = label.dataset.uprightX;
            const y = label.dataset.uprightY;
            label.setAttribute('transform', `rotate(${counterRotation.toFixed(3)},${x},${y})`);
        }
    }

    function animate(startRotation, destinationRotation, startTime) {
        const tick = (timestamp) => {
            if (!_svgEl) { _rafId = null; return; }

            const elapsed = timestamp - startTime;
            const progress = smoothStep(elapsed / DURATION_MS);
            _currentRotation = normalizeDegrees(startRotation + (destinationRotation - startRotation) * progress);
            applyRotation(_currentRotation);

            if (progress < 1 && Math.abs(shortestAngularDelta(_currentRotation, _targetRotation)) > EPSILON) {
                _rafId = requestAnimationFrame(tick);
                return;
            }

            _currentRotation = normalizeDegrees(_targetRotation);
            applyRotation(_currentRotation);
            _rafId = null;
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('OnJsGraphRotationSettled', _currentRotation);
        };

        _rafId = requestAnimationFrame(tick);
    }

    return {
        init(svgEl, dotNetRef, rotationDegrees) {
            _svgEl = svgEl;
            _dotNetRef = dotNetRef;
            _currentRotation = normalizeDegrees(Number(rotationDegrees) || 0);
            _targetRotation = _currentRotation;
            refreshElements();
            applyRotation(_currentRotation);
        },
        setTarget(rotationDegrees) {
            const target = normalizeDegrees(Number(rotationDegrees) || 0);
            _targetRotation = target;

            if (!_svgEl) {
                _currentRotation = target;
                return;
            }

            if (_rafId) {
                cancelAnimationFrame(_rafId);
                _rafId = null;
            }

            const delta = shortestAngularDelta(_currentRotation, target);
            if (Math.abs(delta) <= EPSILON) {
                _currentRotation = target;
                applyRotation(_currentRotation);
                if (_dotNetRef) _dotNetRef.invokeMethodAsync('OnJsGraphRotationSettled', _currentRotation);
                return;
            }

            animate(_currentRotation, _currentRotation + delta, performance.now());
        },
        dispose() {
            if (_rafId) { cancelAnimationFrame(_rafId); _rafId = null; }
            _svgEl = null;
            _rotationLayer = null;
            _uprightLabels = [];
            _dotNetRef = null;
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
