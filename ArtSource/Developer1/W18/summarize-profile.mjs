import fs from "node:fs";

const [csvPath, logPath, outputPath] = process.argv.slice(2);
if (!csvPath || !logPath || !outputPath) {
    throw new Error("Usage: node summarize-profile.mjs frames.csv player.log report.json");
}
const rows = fs.readFileSync(csvPath, "utf8").trim().split(/\r?\n/).slice(1).map(line => {
    const [lap, region, index, frame, wall, cpu, main, render, gpu, draws, batches, setpass, triangles, shader] = line.split(",");
    return {lap, region, index: +index, frame: +frame, wall: +wall, cpu: +cpu, main: +main, render: +render,
        gpu: +gpu, draws: +draws, batches: +batches, setpass: +setpass, triangles: +triangles, shader: +shader};
});
if (rows.length === 0 || rows.some(row => !row.lap || !row.region || !Number.isFinite(row.frame))) {
    throw new Error("Empty or invalid Windows profile");
}
const percentile = (sorted, factor) => sorted[Math.min(sorted.length - 1, Math.ceil(sorted.length * factor) - 1)];
const series = (samples, field, allowZero = false) => {
    const sorted = samples.map(row => row[field]).filter(value => Number.isFinite(value) &&
        (allowZero ? value >= 0 : value > 0)).sort((a, b) => a - b);
    if (!sorted.length) return null;
    const number = factor => +percentile(sorted, factor).toFixed(3);
    return {n: sorted.length, p50: number(.5), p95: number(.95), p99: number(.99),
        max: +sorted.at(-1).toFixed(3)};
};
const summarize = group => ({
    frames: group.length,
    wallMs: series(group, "wall"),
    cpuMs: series(group, "cpu"),
    mainMs: series(group, "main"),
    renderMs: series(group, "render"),
    gpuMs: series(group, "gpu"),
    drawCalls: series(group, "draws", true),
    batches: series(group, "batches", true),
    setPassCalls: series(group, "setpass", true),
    triangles: series(group, "triangles", true),
    shaderNs: series(group, "shader", true),
    shaderMarkedFrames: group.some(row => row.shader >= 0) ? group.filter(row => row.shader > 0).length : null,
    over16_667Ms: group.filter(row => row.wall > 1000 / 60).length
});
const log = fs.readFileSync(logPath, "utf8");
const memory = [...log.matchAll(/^W18_MEMORY ([^,]+),([^,]+),(\d+),(\d+),(\d+),(\d+)/gm)].map(entry => ({
    lap: entry[1], region: entry[2], unityAllocatedBytes: +entry[3], unityReservedBytes: +entry[4],
    textureBytes: +entry[5], workingSetBytes: +entry[6]
}));
const laps = {};
for (const lap of new Set(rows.map(row => row.lap))) {
    const lapRows = rows.filter(row => row.lap === lap);
    const regions = {};
    for (const region of new Set(lapRows.map(row => row.region))) {
        const group = lapRows.filter(row => row.region === region);
        regions[region] = {...summarize(group), transition: summarize(group.filter(row => row.index < 16))};
    }
    laps[lap] = {route: summarize(lapRows), regions};
}
const config = log.match(/^W18_CONFIG .*$/m)?.[0];
if (!config || !log.includes("W18_PROFILE_PASS")) throw new Error("The Windows profile did not finish");
const summary = {config, laps, memory};
fs.writeFileSync(outputPath, JSON.stringify(summary, null, 2) + "\n");
console.log(config);
for (const [name, data] of Object.entries(laps)) {
    const route = data.route;
    console.log(name, "frames", route.frames, "p50/p95/p99 ms",
        route.wallMs?.p50, route.wallMs?.p95, route.wallMs?.p99, "CPU render p95", route.renderMs?.p95,
        "GPU p95", route.gpuMs?.p95, "draws p95", route.drawCalls?.p95, ">16.67ms", route.over16_667Ms);
    for (const [name, area] of Object.entries(data.regions)) {
        console.log(" ", name, "wall p95", area.wallMs?.p95, "render p95", area.renderMs?.p95,
            "GPU p95", area.gpuMs?.p95, "draws p95", area.drawCalls?.p95, "transition max", area.transition.wallMs?.max);
    }
}
