(() => {
    "use strict";

    const CANVAS_WIDTH = 540;
    const CANVAS_HEIGHT = 64;
    const INACTIVITY_TIMEOUT_MS = 20000;
    const PROXIMITY_MARGIN = 56;
    const COMPACT_BREAKPOINT = 960;
    const ITEM_DELAYS = [0.16, 0, 0.16];
    const MAX_ITEM_DELAY = 0.16;
    const BULGE_IMPULSE = 95;
    const BULGE_STIFFNESS = 34;
    const BULGE_DAMPING = 3.4;
    const INACTIVE_WAVE_AGE = 99;

    const ITEM_COLORS = [
        [0.965, 0.306, 0.384],
        [1.0, 0.659, 0.0],
        [0.196, 0.6, 1.0]
    ];

    const COLLAPSED_LAYOUT = {
        idleWidth: 104,
        activeWidth: 126,
        idleHeight: 28,
        activeHeight: 32,
        dotRadius: 4.5,
        activeDotRadius: 10.5,
        wideDotHalfWidth: 14.5
    };

    const WIDE_LAYOUT = { width: 480, height: 46, itemWidth: 140, itemHeight: 34, itemSpacing: 150 };
    const COMPACT_LAYOUT = { width: 206, height: 46, itemWidth: 56, itemHeight: 34, itemSpacing: 64 };

    const VERTEX_SHADER = `
        attribute vec2 a_position;
        void main() {
            gl_Position = vec4(a_position, 0.0, 1.0);
        }`;

    const FRAGMENT_SHADER = `
        precision highp float;

        uniform vec2 u_resolution;
        uniform float u_pixelRatio;
        uniform float u_time;
        uniform float u_energy;
        uniform vec2 u_islandCenter;
        uniform vec2 u_islandHalfSize;
        uniform vec4 u_items[3];
        uniform vec3 u_itemColors[3];
        uniform float u_itemActivity[3];
        uniform float u_itemPulse[3];
        uniform float u_itemReveal[3];
        uniform float u_itemBulge[3];
        uniform float u_itemWaveAge[3];

        const vec3 SURFACE_COLOR = vec3(0.118, 0.118, 0.18);
        const vec3 PRIMARY_COLOR = vec3(0.467, 0.42, 0.906);
        const vec3 SECONDARY_COLOR = vec3(1.0, 0.43, 0.78);

        vec3 permute(vec3 value) {
            return mod(((value * 34.0) + 1.0) * value, 289.0);
        }

        float simplexNoise(vec2 value) {
            const vec4 skew = vec4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
            vec2 cell = floor(value + dot(value, skew.yy));
            vec2 offset0 = value - cell + dot(cell, skew.xx);
            vec2 cornerStep = (offset0.x > offset0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
            vec4 offsets = offset0.xyxy + skew.xxzz;
            offsets.xy -= cornerStep;
            cell = mod(cell, 289.0);
            vec3 gradientIndex = permute(permute(cell.y + vec3(0.0, cornerStep.y, 1.0)) + cell.x + vec3(0.0, cornerStep.x, 1.0));
            vec3 falloff = max(0.5 - vec3(dot(offset0, offset0), dot(offsets.xy, offsets.xy), dot(offsets.zw, offsets.zw)), 0.0);
            falloff = falloff * falloff;
            falloff = falloff * falloff;
            vec3 gradientX = 2.0 * fract(gradientIndex * skew.www) - 1.0;
            vec3 gradientHeight = abs(gradientX) - 0.5;
            vec3 gradientOffset = floor(gradientX + 0.5);
            vec3 gradientA = gradientX - gradientOffset;
            falloff *= 1.79284291400159 - 0.85373472095314 * (gradientA * gradientA + gradientHeight * gradientHeight);
            vec3 gradient;
            gradient.x = gradientA.x * offset0.x + gradientHeight.x * offset0.y;
            gradient.yz = gradientA.yz * offsets.xz + gradientHeight.yz * offsets.yw;
            return 130.0 * dot(falloff, gradient);
        }

        float roundedBoxDistance(vec2 position, vec2 halfSize, float radius) {
            vec2 corner = abs(position) - halfSize + radius;
            return length(max(corner, 0.0)) + min(max(corner.x, corner.y), 0.0) - radius;
        }

        float islandDeformation(vec2 position) {
            float deformation = 0.0;

            for (int index = 0; index < 3; index++) {
                float offset = position.x - u_items[index].x;
                float age = u_itemWaveAge[index];
                float travel = abs(offset) - age * 95.0;

                deformation += u_itemBulge[index] * exp(-offset * offset / 1100.0);
                deformation += sin(travel * 0.2) * exp(-travel * travel / 400.0) * exp(-age * 1.4) * smoothstep(0.0, 0.15, age) * 2.5;
            }

            return deformation;
        }

        float islandDistance(vec2 position) {
            vec2 local = position - u_islandCenter;
            float averageReveal = (u_itemReveal[0] + u_itemReveal[1] + u_itemReveal[2]) / 3.0;
            float wobble = simplexNoise(local * 0.03 + vec2(u_time * 0.45, -u_time * 0.3))
                * mix(0.7, 2.0, clamp(u_energy, 0.0, 1.4))
                * (1.0 - 0.6 * averageReveal);
            return roundedBoxDistance(local, u_islandHalfSize, min(u_islandHalfSize.x, u_islandHalfSize.y)) + wobble
                - islandDeformation(position) * (1.0 - 0.55 * averageReveal);
        }

        float itemDistance(vec2 position, vec4 item, float seed, float wobbleAmount) {
            vec2 local = position - item.xy;
            float wobble = simplexNoise(local * 0.09 + vec2(u_time * 0.6 + seed, -u_time * 0.45)) * wobbleAmount;
            return roundedBoxDistance(local, item.zw, min(item.z, item.w)) + wobble;
        }

        vec3 scene(vec2 refractedPosition, vec2 straightPosition, float antialias) {
            vec3 color = SURFACE_COLOR * 0.85;
            float blobStrength = 0.24 + 0.2 * u_energy;

            for (int blobIndex = 0; blobIndex < 3; blobIndex++) {
                float phase = float(blobIndex) * 2.094;
                vec2 blobCenter = u_islandCenter + vec2(
                    sin(u_time * 0.55 + phase) * u_islandHalfSize.x * 0.75,
                    cos(u_time * 0.41 + phase * 1.3) * u_islandHalfSize.y * 0.6);
                float blobRadius = u_islandHalfSize.y * (1.1 + 0.3 * sin(u_time * 0.7 + phase));
                vec2 delta = (refractedPosition - blobCenter) / vec2(blobRadius * 1.8, blobRadius);
                float isSecondary = blobIndex == 1 ? 1.0 : 0.0;
                vec3 blobColor = mix(PRIMARY_COLOR, SECONDARY_COLOR, isSecondary * 0.6);
                color += blobColor * exp(-dot(delta, delta)) * blobStrength * mix(1.0, 0.5, isSecondary);
            }

            for (int index = 0; index < 3; index++) {
                vec4 item = u_items[index];
                float reveal = u_itemReveal[index];
                vec2 position = mix(refractedPosition, straightPosition, mix(0.65, 0.85, reveal));
                float wobbleAmount = (0.2 + 0.2 * u_itemActivity[index] + 0.7 * u_itemPulse[index]) * (1.0 - 0.8 * reveal);
                float distance = itemDistance(position, item, float(index) * 3.7, wobbleAmount);
                float coverage = 1.0 - smoothstep(-antialias, antialias, distance);
                vec3 fullColor = u_itemColors[index];
                vec3 mutedColor = mix(fullColor, vec3(0.42, 0.42, 0.52), 0.6) * 0.8;
                vec3 itemColor = mix(mix(mutedColor, fullColor, u_itemActivity[index]), fullColor, reveal);

                float breathing = 0.8 + 0.2 * sin(u_time * 2.4 + float(index) * 1.9);
                vec3 glassTint = mix(color, fullColor, 0.3);

                color = mix(color, mix(itemColor, glassTint, smoothstep(0.2, 0.8, reveal)), coverage);

                float glow = exp(-max(distance, 0.0) * 0.14)
                    * (0.6 * u_itemActivity[index] * breathing + 0.8 * u_itemPulse[index])
                    * (1.0 - reveal);
                color += fullColor * glow * (1.0 - coverage);
            }

            return color;
        }

        void main() {
            vec2 fragment = vec2(gl_FragCoord.x, u_resolution.y * u_pixelRatio - gl_FragCoord.y) / u_pixelRatio;
            float antialias = 1.0 / u_pixelRatio;
            float distance = islandDistance(fragment);

            if (distance > 30.0) {
                gl_FragColor = vec4(0.0);
                return;
            }

            vec2 epsilon = vec2(0.75, 0.0);
            vec2 normal = normalize(vec2(
                islandDistance(fragment + epsilon.xy) - islandDistance(fragment - epsilon.xy),
                islandDistance(fragment + epsilon.yx) - islandDistance(fragment - epsilon.yx)) + 0.00001);

            float averageReveal = (u_itemReveal[0] + u_itemReveal[1] + u_itemReveal[2]) / 3.0;
            float bevel = min(u_islandHalfSize.y * 0.8, 12.0);
            float edge = 1.0 - smoothstep(0.0, bevel, max(-distance, 0.0));
            float edgeCurve = edge * edge;

            vec2 ripple = vec2(0.0);
            for (int index = 0; index < 3; index++) {
                float pulse = u_itemPulse[index];
                vec2 toItem = fragment - u_items[index].xy;
                float radius = length(toItem);
                float front = (1.0 - pulse) * 70.0;
                float wave = sin((radius - front) * 0.45) * exp(-pow((radius - front) / 9.0, 2.0)) * pulse;
                ripple += (toItem / max(radius, 1.0)) * wave * 3.5;
            }

            vec2 refracted = fragment - normal * edgeCurve * mix(4.0, 3.0, averageReveal) + ripple;
            vec2 dispersion = -normal * edgeCurve * 0.6;

            vec3 glass = vec3(
                scene(refracted + dispersion, fragment, antialias).r,
                scene(refracted, fragment, antialias).g,
                scene(refracted - dispersion, fragment, antialias).b);

            float vertical = clamp((fragment.y - (u_islandCenter.y - u_islandHalfSize.y)) / (2.0 * u_islandHalfSize.y), 0.0, 1.0);
            glass += vec3(1.0) * (1.0 - vertical) * 0.03;
            glass *= mix(1.0, 0.94, smoothstep(0.55, 1.0, vertical) * edge);
            glass += mix(PRIMARY_COLOR, vec3(1.0), 0.5) * edgeCurve * 0.07;

            vec2 islandLocal = fragment - u_islandCenter;
            float sweepTravel = u_islandHalfSize.x * 2.0 + 60.0;
            float sweepPosition = mod(u_time * 22.0, sweepTravel + 120.0) - sweepTravel * 0.5 - 30.0;
            float sweep = exp(-pow((islandLocal.x + islandLocal.y * 0.8 - sweepPosition) / 9.0, 2.0));
            glass += vec3(1.0) * sweep * 0.06;

            float lightAngle = -2.2 + sin(u_time * 0.4) * 0.6;
            vec2 lightDirection = vec2(cos(lightAngle), sin(lightAngle));
            float edgeCube = edgeCurve * edge;
            float specular = pow(clamp(dot(normal, lightDirection), 0.0, 1.0), 4.0) * edgeCube;
            float backLight = pow(clamp(dot(normal, -lightDirection), 0.0, 1.0), 4.0) * edgeCube;
            glass += vec3(1.0) * specular * (0.3 + 0.15 * u_energy);
            glass += PRIMARY_COLOR * backLight * 0.18;

            float outline = 1.0 - smoothstep(0.0, 1.0 + antialias, abs(distance + 0.5));
            float outlineLight = 0.6 + 0.4 * clamp(dot(normal, lightDirection) * 0.5 + 0.5, 0.0, 1.0);
            glass += vec3(1.0) * outline * outlineLight * 0.42;

            float itemOpacity = 0.0;
            vec3 itemGlowColor = vec3(0.0);
            float itemGlow = 0.0;

            for (int index = 0; index < 3; index++) {
                float reveal = u_itemReveal[index];
                vec4 item = u_items[index];
                vec3 itemColor = u_itemColors[index];
                float itemEdgeDistance = itemDistance(fragment, item, float(index) * 3.7, 0.0);
                float itemOutline = 1.0 - smoothstep(0.0, 1.0 + antialias, abs(itemEdgeDistance + 0.5));
                float itemTop = clamp(0.5 - (fragment.y - item.y) / max(item.w, 1.0) * 0.5, 0.0, 1.0);
                float itemInside = 1.0 - smoothstep(-antialias, antialias, itemEdgeDistance);
                float itemRim = (1.0 - smoothstep(0.0, 6.0, max(-itemEdgeDistance, 0.0))) * itemInside;

                glass += itemColor * itemOutline * reveal * (0.55 + 0.45 * itemTop);
                glass += vec3(1.0) * itemOutline * itemTop * 0.08 * reveal;
                glass += itemColor * itemRim * 0.06 * reveal;
                itemOpacity = max(itemOpacity, itemInside * mix(1.0, 0.9, reveal));

                float itemBreathing = 0.8 + 0.2 * sin(u_time * 2.4 + float(index) * 1.9);
                float bleed = exp(-max(itemEdgeDistance, 0.0) * 0.12)
                    * (0.35 * u_itemActivity[index] * itemBreathing + 0.6 * u_itemPulse[index])
                    * (1.0 - reveal);
                itemGlowColor += itemColor * bleed;
                itemGlow += bleed;
            }

            float coverage = 1.0 - smoothstep(-antialias, antialias, distance);
            float bodyAlpha = max(mix(0.62, 0.95, edge), itemOpacity) * coverage;
            float glow = exp(-max(distance, 0.0) * 0.22) * (0.05 + 0.2 * u_energy) * (1.0 - coverage);
            float shadow = exp(-max(islandDistance(fragment - vec2(0.0, 3.0)), 0.0) * 0.3) * 0.3 * (1.0 - coverage);

            float outsideGlow = clamp(itemGlow, 0.0, 1.0) * (1.0 - coverage);

            vec3 premultipliedColor = glass * bodyAlpha + PRIMARY_COLOR * glow + itemGlowColor * (1.0 - coverage);
            gl_FragColor = vec4(premultipliedColor, clamp(bodyAlpha + glow + shadow + outsideGlow, 0.0, 1.0));
        }`;

    const instances = new Map();

    function clamp01(value) {
        return Math.min(1, Math.max(0, value));
    }

    function lerp(from, to, amount) {
        return from + (to - from) * amount;
    }

    function smoothStep(edge0, edge1, value) {
        const amount = clamp01((value - edge0) / (edge1 - edge0));
        return amount * amount * (3 - 2 * amount);
    }

    function easeInOutCubic(value) {
        return value < 0.5 ? 4 * value * value * value : 1 - Math.pow(-2 * value + 2, 3) / 2;
    }

    function createSpring(value) {
        return { value, velocity: 0 };
    }

    function stepSpring(spring, target, stiffness, damping, deltaSeconds) {
        const force = (target - spring.value) * stiffness - spring.velocity * damping;
        spring.velocity += force * deltaSeconds;
        spring.value += spring.velocity * deltaSeconds;
    }

    function compileShader(gl, type, source) {
        const shader = gl.createShader(type);
        gl.shaderSource(shader, source);
        gl.compileShader(shader);

        if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
            console.error("Notification island shader failed to compile.", gl.getShaderInfoLog(shader));
            gl.deleteShader(shader);
            return null;
        }

        return shader;
    }

    function setupGraphics(instance) {
        const gl = instance.canvas.getContext("webgl", { alpha: true, premultipliedAlpha: true, antialias: false });

        if (!gl) {
            instance.root.classList.add("notification-island-fallback");
            return;
        }

        const vertexShader = compileShader(gl, gl.VERTEX_SHADER, VERTEX_SHADER);
        const fragmentShader = compileShader(gl, gl.FRAGMENT_SHADER, FRAGMENT_SHADER);

        if (!vertexShader || !fragmentShader) {
            instance.root.classList.add("notification-island-fallback");
            return;
        }

        const program = gl.createProgram();
        gl.attachShader(program, vertexShader);
        gl.attachShader(program, fragmentShader);
        gl.linkProgram(program);

        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
            console.error("Notification island shader program failed to link.", gl.getProgramInfoLog(program));
            instance.root.classList.add("notification-island-fallback");
            return;
        }

        const buffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);

        const positionLocation = gl.getAttribLocation(program, "a_position");
        gl.enableVertexAttribArray(positionLocation);
        gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

        gl.useProgram(program);

        const uniformNames = [
            "u_resolution", "u_pixelRatio", "u_time", "u_energy", "u_islandCenter", "u_islandHalfSize",
            "u_items", "u_itemColors", "u_itemActivity", "u_itemPulse", "u_itemReveal", "u_itemBulge", "u_itemWaveAge"
        ];

        instance.uniforms = {};
        uniformNames.forEach((name) => instance.uniforms[name] = gl.getUniformLocation(program, name));

        gl.uniform3fv(instance.uniforms.u_itemColors, new Float32Array(ITEM_COLORS.flat()));

        instance.gl = gl;
        instance.root.classList.remove("notification-island-fallback");
        resizeCanvas(instance);
    }

    function resizeCanvas(instance) {
        instance.pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
        instance.canvas.width = Math.round(CANVAS_WIDTH * instance.pixelRatio);
        instance.canvas.height = Math.round(CANVAS_HEIGHT * instance.pixelRatio);
        instance.isCompact = window.innerWidth < COMPACT_BREAKPOINT;
        instance.root.classList.toggle("notification-island-compact", instance.isCompact);
    }

    function computeGeometry(instance) {
        const layout = instance.isCompact ? COMPACT_LAYOUT : WIDE_LAYOUT;
        const expansion = instance.expansion.value;
        const clampedExpansion = clamp01(expansion);
        const collapsedWidth = instance.collapsedWidth.value;
        const collapsedHeight = lerp(COLLAPSED_LAYOUT.idleHeight, COLLAPSED_LAYOUT.activeHeight,
            clamp01((collapsedWidth - COLLAPSED_LAYOUT.idleWidth) / (COLLAPSED_LAYOUT.activeWidth - COLLAPSED_LAYOUT.idleWidth)));
        const dotSpacing = (collapsedWidth - 20) / 3;
        const motionScale = instance.reducedMotion ? 0.3 : 1;
        const time = instance.time;

        const islandHalfHeight = lerp(collapsedHeight, layout.height, expansion) / 2;
        const islandHalfWidth = Math.max(lerp(collapsedWidth, layout.width, expansion) / 2, islandHalfHeight);

        const items = [0, 1, 2].map((index) => {
            const reveal = easeInOutCubic(clamp01((clampedExpansion - ITEM_DELAYS[index]) / (1 - MAX_ITEM_DELAY)));
            const drift = (1 - reveal) * motionScale;
            const driftX = (Math.sin(time * 0.9 + index * 2.1) * 1.3 + Math.sin(time * 0.43 + index * 4.7) * 0.7) * drift;
            const driftY = Math.cos(time * 0.75 + index * 1.7) * 1.1 * drift;
            const dotRadius = instance.dotRadii[index].value;
            const dotHalfWidth = Math.max(dotRadius, instance.dotHalfWidths[index].value);

            return {
                centerX: CANVAS_WIDTH / 2 + lerp((index - 1) * dotSpacing + driftX, (index - 1) * layout.itemSpacing, reveal),
                centerY: CANVAS_HEIGHT / 2 + driftY,
                halfWidth: lerp(dotHalfWidth, layout.itemWidth / 2, reveal),
                halfHeight: lerp(dotRadius, layout.itemHeight / 2, reveal),
                reveal
            };
        });

        return {
            centerX: CANVAS_WIDTH / 2,
            centerY: CANVAS_HEIGHT / 2,
            islandHalfWidth,
            islandHalfHeight,
            items
        };
    }

    function syncDom(instance, geometry) {
        const hitArea = instance.hitArea;
        hitArea.style.width = `${geometry.islandHalfWidth * 2}px`;
        hitArea.style.height = `${geometry.islandHalfHeight * 2}px`;
        hitArea.style.transform = `translate(${geometry.centerX - geometry.islandHalfWidth}px, ${geometry.centerY - geometry.islandHalfHeight}px)`;

        instance.items.forEach((itemElement, index) => {
            const item = geometry.items[index];
            itemElement.style.width = `${item.halfWidth * 2}px`;
            itemElement.style.height = `${item.halfHeight * 2}px`;
            itemElement.style.transform = `translate(${item.centerX - item.halfWidth}px, ${item.centerY - item.halfHeight}px)`;

            const countElement = itemElement.querySelector(".notification-island-count");
            if (countElement) {
                countElement.style.opacity = `${1 - smoothStep(0, 0.3, item.reveal)}`;
            }

            const buttonElement = itemElement.querySelector(".notification-island-button");
            if (buttonElement) {
                buttonElement.style.opacity = `${smoothStep(0.6, 1, item.reveal)}`;
                buttonElement.style.pointerEvents = item.reveal > 0.9 ? "auto" : "none";
                buttonElement.tabIndex = item.reveal > 0.9 ? 0 : -1;
            }
        });
    }

    function render(instance, geometry, energy) {
        const gl = instance.gl;
        const uniforms = instance.uniforms;

        gl.viewport(0, 0, instance.canvas.width, instance.canvas.height);
        gl.uniform2f(uniforms.u_resolution, CANVAS_WIDTH, CANVAS_HEIGHT);
        gl.uniform1f(uniforms.u_pixelRatio, instance.pixelRatio);
        gl.uniform1f(uniforms.u_time, instance.time);
        gl.uniform1f(uniforms.u_energy, energy);
        gl.uniform2f(uniforms.u_islandCenter, geometry.centerX, geometry.centerY);
        gl.uniform2f(uniforms.u_islandHalfSize, geometry.islandHalfWidth, geometry.islandHalfHeight);
        gl.uniform4fv(uniforms.u_items, new Float32Array(geometry.items.flatMap((item) => [item.centerX, item.centerY, item.halfWidth, item.halfHeight])));
        gl.uniform1fv(uniforms.u_itemActivity, new Float32Array(instance.itemActivity));
        gl.uniform1fv(uniforms.u_itemPulse, new Float32Array(instance.itemPulse));
        gl.uniform1fv(uniforms.u_itemReveal, new Float32Array(geometry.items.map((item) => item.reveal)));
        gl.uniform1fv(uniforms.u_itemBulge, new Float32Array(instance.bulges.map((bulge) => bulge.value)));
        gl.uniform1fv(uniforms.u_itemWaveAge, new Float32Array(instance.waveAges));
        gl.drawArrays(gl.TRIANGLES, 0, 3);
    }

    function frame(instance, now) {
        if (instance.isDisposed) {
            return;
        }

        const deltaSeconds = Math.min((now - instance.lastFrame) / 1000, 1 / 20);
        instance.lastFrame = now;

        const hasNotifications = instance.counts.some((count) => count > 0);

        instance.energyPulse *= Math.exp(-deltaSeconds / 2.2);

        for (let index = 0; index < 3; index++) {
            instance.itemPulse[index] *= Math.exp(-deltaSeconds / 1.6);
            const activityTarget = instance.counts[index] > 0 ? 1 : 0;
            instance.itemActivity[index] += (activityTarget - instance.itemActivity[index]) * (1 - Math.exp(-deltaSeconds * 3));

            const count = instance.counts[index];
            stepSpring(instance.dotRadii[index], count > 0 ? COLLAPSED_LAYOUT.activeDotRadius : COLLAPSED_LAYOUT.dotRadius, 160, 14, deltaSeconds);
            stepSpring(instance.dotHalfWidths[index], count > 9 ? COLLAPSED_LAYOUT.wideDotHalfWidth : 0, 160, 14, deltaSeconds);
            stepSpring(instance.bulges[index], 0, BULGE_STIFFNESS, BULGE_DAMPING, deltaSeconds);
            instance.waveAges[index] = Math.min(instance.waveAges[index] + deltaSeconds, INACTIVE_WAVE_AGE);
        }

        stepSpring(instance.expansion, instance.isExpanded ? 1 : 0, 70, 11, deltaSeconds);
        stepSpring(instance.collapsedWidth, hasNotifications ? COLLAPSED_LAYOUT.activeWidth : COLLAPSED_LAYOUT.idleWidth, 90, 13, deltaSeconds);

        const motionScale = instance.reducedMotion ? 0.35 : 1;
        const energy = ((hasNotifications ? 0.5 : 0.14) + instance.energyPulse * 0.9 + clamp01(instance.expansion.value) * 0.08) * motionScale;
        instance.time += deltaSeconds * (0.8 + 0.8 * energy) * (instance.reducedMotion ? 0.4 : 1);

        if (instance.isExpanded && !instance.isSectionOpen && now - instance.lastInteraction > INACTIVITY_TIMEOUT_MS) {
            collapse(instance);
        }

        const geometry = computeGeometry(instance);
        instance.lastGeometry = geometry;

        syncDom(instance, geometry);

        if (instance.gl && !instance.isContextLost) {
            render(instance, geometry, energy);
        }

        instance.animationFrame = requestAnimationFrame((time) => frame(instance, time));
    }

    function registerInteraction(instance) {
        instance.lastInteraction = performance.now();
    }

    function expand(instance) {
        if (instance.isExpanded) {
            registerInteraction(instance);
            return;
        }

        instance.isExpanded = true;
        instance.energyPulse = Math.max(instance.energyPulse, 0.4);
        instance.root.classList.add("notification-island-expanded");
        registerInteraction(instance);
    }

    function collapse(instance) {
        if (!instance.isExpanded || instance.isSectionOpen) {
            return;
        }

        instance.isExpanded = false;
        instance.energyPulse = Math.max(instance.energyPulse, 0.25);
        instance.root.classList.remove("notification-island-expanded");
    }

    function isPointerNearIsland(instance, clientX, clientY) {
        const geometry = instance.lastGeometry;

        if (!geometry) {
            return false;
        }

        const bounds = instance.root.getBoundingClientRect();
        const left = bounds.left + geometry.centerX - geometry.islandHalfWidth - PROXIMITY_MARGIN;
        const right = bounds.left + geometry.centerX + geometry.islandHalfWidth + PROXIMITY_MARGIN;
        const top = bounds.top + geometry.centerY - geometry.islandHalfHeight - PROXIMITY_MARGIN;
        const bottom = bounds.top + geometry.centerY + geometry.islandHalfHeight + PROXIMITY_MARGIN;

        return clientX >= left && clientX <= right && clientY >= top && clientY <= bottom;
    }

    function applyState(instance, state) {
        if (!state) {
            return;
        }

        if (Array.isArray(state.counts)) {
            instance.counts = [0, 1, 2].map((index) => Number(state.counts[index]) || 0);
        }

        if (Array.isArray(state.pulseIndices) && state.pulseIndices.length > 0) {
            state.pulseIndices.forEach((index) => {
                if (index >= 0 && index < 3) {
                    instance.itemPulse[index] = 1;
                    instance.bulges[index].velocity += BULGE_IMPULSE * (instance.reducedMotion ? 0.35 : 1);
                    instance.waveAges[index] = 0;
                }
            });

            instance.energyPulse = 1;
        }
    }

    function create(root, state) {
        if (!root || instances.has(root)) {
            return;
        }

        const instance = {
            root,
            canvas: root.querySelector(".notification-island-canvas"),
            hitArea: root.querySelector(".notification-island-hit"),
            items: Array.from(root.querySelectorAll(".notification-island-item")),
            counts: [0, 0, 0],
            itemActivity: [0, 0, 0],
            itemPulse: [0, 0, 0],
            dotRadii: [0, 1, 2].map(() => createSpring(COLLAPSED_LAYOUT.dotRadius)),
            dotHalfWidths: [0, 1, 2].map(() => createSpring(0)),
            expansion: createSpring(0),
            bulges: [0, 1, 2].map(() => createSpring(0)),
            waveAges: [INACTIVE_WAVE_AGE, INACTIVE_WAVE_AGE, INACTIVE_WAVE_AGE],
            collapsedWidth: createSpring(COLLAPSED_LAYOUT.idleWidth),
            energyPulse: 0,
            isExpanded: false,
            isSectionOpen: false,
            isCompact: false,
            isDisposed: false,
            isContextLost: false,
            lastInteraction: performance.now(),
            lastFrame: performance.now(),
            time: Math.random() * 100,
            pixelRatio: 1,
            gl: null,
            uniforms: null,
            lastGeometry: null,
            reducedMotion: window.matchMedia("(prefers-reduced-motion: reduce)").matches
        };

        applyState(instance, state);

        instance.onHitAreaClick = () => expand(instance);
        instance.onRootInteraction = () => registerInteraction(instance);
        instance.onKeyDown = (event) => {
            registerInteraction(instance);

            if (event.key === "Escape") {
                collapse(instance);
            }
        };
        instance.onPointerMove = (event) => {
            if (instance.isExpanded && isPointerNearIsland(instance, event.clientX, event.clientY)) {
                registerInteraction(instance);
            }
        };
        instance.onDocumentPointerDown = (event) => {
            const header = root.closest("header") || root.parentElement;
            const target = event.target;

            if (!instance.isExpanded || !header || !header.contains(target)) {
                return;
            }

            const isInsideIsland = instance.hitArea.contains(target) || instance.items.some((item) => item.contains(target));

            if (!isInsideIsland) {
                collapse(instance);
            }
        };
        instance.onResize = () => resizeCanvas(instance);
        instance.onContextLost = (event) => {
            event.preventDefault();
            instance.isContextLost = true;
        };
        instance.onContextRestored = () => {
            instance.isContextLost = false;
            setupGraphics(instance);
        };

        instance.hitArea.addEventListener("click", instance.onHitAreaClick);
        root.addEventListener("pointerdown", instance.onRootInteraction);
        root.addEventListener("focusin", instance.onRootInteraction);
        root.addEventListener("keydown", instance.onKeyDown);
        document.addEventListener("pointermove", instance.onPointerMove, { passive: true });
        document.addEventListener("pointerdown", instance.onDocumentPointerDown);
        window.addEventListener("resize", instance.onResize);
        instance.canvas.addEventListener("webglcontextlost", instance.onContextLost);
        instance.canvas.addEventListener("webglcontextrestored", instance.onContextRestored);

        setupGraphics(instance);
        resizeCanvas(instance);

        instances.set(root, instance);
        instance.animationFrame = requestAnimationFrame((time) => frame(instance, time));
    }

    function update(root, state) {
        const instance = instances.get(root);

        if (instance) {
            applyState(instance, state);
        }
    }

    function setSectionOpen(root, isOpen) {
        const instance = instances.get(root);

        if (!instance) {
            return;
        }

        instance.isSectionOpen = Boolean(isOpen);
        registerInteraction(instance);
    }

    function dispose(root) {
        const instance = instances.get(root);

        if (!instance) {
            return;
        }

        instance.isDisposed = true;
        cancelAnimationFrame(instance.animationFrame);

        instance.hitArea.removeEventListener("click", instance.onHitAreaClick);
        root.removeEventListener("pointerdown", instance.onRootInteraction);
        root.removeEventListener("focusin", instance.onRootInteraction);
        root.removeEventListener("keydown", instance.onKeyDown);
        document.removeEventListener("pointermove", instance.onPointerMove);
        document.removeEventListener("pointerdown", instance.onDocumentPointerDown);
        window.removeEventListener("resize", instance.onResize);
        instance.canvas.removeEventListener("webglcontextlost", instance.onContextLost);
        instance.canvas.removeEventListener("webglcontextrestored", instance.onContextRestored);

        const loseContextExtension = instance.gl && instance.gl.getExtension("WEBGL_lose_context");
        if (loseContextExtension) {
            loseContextExtension.loseContext();
        }

        instances.delete(root);
    }

    window.notificationIsland = { create, update, setSectionOpen, dispose };
})();
