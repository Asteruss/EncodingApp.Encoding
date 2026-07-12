import { useState, useEffect, useMemo, useRef } from 'react';
import Chart from 'chart.js/auto';

const HISTORY_STORAGE_KEY = 'compression_history';

const loadHistory = () => {
    try {
        const raw = localStorage.getItem(HISTORY_STORAGE_KEY);
        const parsed = raw ? JSON.parse(raw) : [];
        return Array.isArray(parsed) ? parsed : [];
    } catch (err) {
        console.error('Не удалось прочитать историю из localStorage:', err);
        return [];
    }
};

const formatDate = (iso) => {
    try {
        return new Date(iso).toLocaleString();
    } catch {
        return iso;
    }
};

// Ключ для ГРУППИРОВКИ (внутренний, всегда по сырым id — стабилен и не ломается,
// даже если displayName ещё не загрузился или совпадает у разных алгоритмов)
const chainKey = (chain) =>
    (chain || [])
        .map(step => (typeof step === 'string' ? step : step?.id ?? JSON.stringify(step)))
        .join(' -> ');

// Человекочитаемое название цепочки для ОТОБРАЖЕНИЯ — переводит id через displayName
// из /api/compression/algorithms
const chainDisplayName = (chain, availableAlgorithms) =>
    (chain || [])
        .map(step => {
            const id = typeof step === 'string' ? step : step?.id;
            const matched = availableAlgorithms.find(a => a.id === id);
            return matched?.displayName ?? id ?? JSON.stringify(step);
        })
        .join(' → ');

const average = (items, getter) => {
    const values = items.map(getter).filter(v => typeof v === 'number' && !Number.isNaN(v));
    if (values.length === 0) return null;
    return values.reduce((sum, v) => sum + v, 0) / values.length;
};

const formatNumber = (value, digits = 2) => {
    if (value === null || value === undefined || Number.isNaN(Number(value))) return 'N/A';
    return Number(value).toFixed(digits);
};

const formatBytes = (bytes) => {
    if (bytes === null || bytes === undefined || Number.isNaN(Number(bytes))) return 'N/A';
    const num = Number(bytes);
    if (Math.abs(num) < 1024) return `${num.toFixed(0)} Б`;
    if (Math.abs(num) < 1024 * 1024) return `${(num / 1024).toFixed(1)} КБ`;
    return `${(num / (1024 * 1024)).toFixed(2)} МБ`;
};

const computeEntryOverall = (entry) => {
    const compression = entry.metrics?.compressionRatio || [];
    if (compression.length === 0) return { overallRatio: null, overallSpaceSavedPercent: null };

    const firstInput = compression[0]?.inputSizeBytes;
    const lastOutput = compression[compression.length - 1]?.outputSizeBytes;

    if (typeof firstInput === 'number' && typeof lastOutput === 'number' && firstInput > 0) {
        const overallRatio = lastOutput / firstInput;
        return { overallRatio, overallSpaceSavedPercent: (1 - overallRatio) * 100 };
    }

    const hasRatios = compression.every(m => typeof m.compressionRatio === 'number');
    if (!hasRatios) return { overallRatio: null, overallSpaceSavedPercent: null };
    const overallRatio = compression.reduce((acc, m) => acc * m.compressionRatio, 1);
    return { overallRatio, overallSpaceSavedPercent: (1 - overallRatio) * 100 };
};

export default function ComparePage() {
    const [history, setHistory] = useState([]);
    const [selectedIds, setSelectedIds] = useState([]);
    const [availableAlgorithms, setAvailableAlgorithms] = useState([]);

    const timeCanvasRef = useRef(null);
    const spaceCanvasRef = useRef(null);
    const timeChartRef = useRef(null);
    const spaceChartRef = useRef(null);

    useEffect(() => {
        setHistory(loadHistory());
    }, []);

    useEffect(() => {
        fetch('/api/compression/algorithms')
            .then(res => {
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                return res.json();
            })
            .then(data => setAvailableAlgorithms(data.algorithms || []))
            .catch(err => console.error('Не удалось загрузить список алгоритмов для перевода имён:', err));
    }, []);

    const getStepDisplayName = (stepName) => {
        if (!stepName) return "N/A";
        const algorithmId = stepName.replace(/_?(Encode|Decode)$/i, '');
        const matched = availableAlgorithms.find(a => a.id === algorithmId);
        return matched?.displayName ?? algorithmId;
    };

    const toggleSelect = (id) => {
        setSelectedIds(prev =>
            prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
        );
    };

    const selectAll = () => setSelectedIds(history.map(h => h.id));
    const clearSelection = () => setSelectedIds([]);

    const selectedEntries = useMemo(
        () => history.filter(h => selectedIds.includes(h.id)),
        [history, selectedIds]
    );

    const groupedByChain = useMemo(() => {
        const groups = {};
        selectedEntries.forEach(entry => {
            const key = chainKey(entry.chain);
            if (!groups[key]) {
                groups[key] = { chain: entry.chain || [], entries: [] };
            }
            groups[key].entries.push(entry);
        });
        return groups;
    }, [selectedEntries]);

    const chainStats = useMemo(() => {
        return Object.entries(groupedByChain).map(([key, group]) => {
            const { chain, entries } = group;

            const stepCount = Math.max(
                0,
                ...entries.map(e => (e.metrics?.timing || []).length)
            );

            const steps = Array.from({ length: stepCount }).map((_, stepIndex) => {
                const timingAtStep = entries.map(e => e.metrics?.timing?.[stepIndex]).filter(Boolean);
                const compressionAtStep = entries.map(e => e.metrics?.compressionRatio?.[stepIndex]).filter(Boolean);
                const gcAtStep = entries.map(e => e.metrics?.gcPressure?.[stepIndex]).filter(Boolean);

                return {
                    stepName: timingAtStep[0]?.stepName || compressionAtStep[0]?.stepName || `Шаг ${stepIndex + 1}`,
                    avgElapsedMilliseconds: average(timingAtStep, m => m.elapsedMilliseconds),
                    avgThroughputMBps: average(timingAtStep, m => m.throughputMBps),
                    avgCompressionRatio: average(compressionAtStep, m => m.compressionRatio),
                    avgSpaceSavedPercent: average(compressionAtStep, m => m.spaceSavedPercent),
                    avgAllocatedBytes: average(gcAtStep, m => m.allocatedBytesDelta),
                    avgGen0: average(gcAtStep, m => m.gen0CollectionsDelta),
                    avgGen1: average(gcAtStep, m => m.gen1CollectionsDelta),
                    avgGen2: average(gcAtStep, m => m.gen2CollectionsDelta),
                };
            });

            const totalAvgElapsedMs = steps.reduce((sum, s) => sum + (s.avgElapsedMilliseconds || 0), 0);

            const entryOveralls = entries.map(computeEntryOverall);
            const avgOverallCompressionRatio = average(entryOveralls, o => o.overallRatio);
            const avgOverallSpaceSavedPercent = average(entryOveralls, o => o.overallSpaceSavedPercent);

            return {
                key,
                chain,
                sampleCount: entries.length,
                steps,
                totalAvgElapsedMs,
                avgOverallCompressionRatio,
                avgOverallSpaceSavedPercent
            };
        });
    }, [groupedByChain]);

    useEffect(() => {
        if (timeChartRef.current) {
            timeChartRef.current.destroy();
            timeChartRef.current = null;
        }
        if (spaceChartRef.current) {
            spaceChartRef.current.destroy();
            spaceChartRef.current = null;
        }

        if (chainStats.length === 0) return;

        const labels = chainStats.map(s => chainDisplayName(s.chain, availableAlgorithms) || '(пустая цепочка)');

        if (timeCanvasRef.current) {
            timeChartRef.current = new Chart(timeCanvasRef.current, {
                type: 'bar',
                data: {
                    labels,
                    datasets: [{
                        label: 'Среднее время (мс)',
                        data: chainStats.map(s => s.totalAvgElapsedMs || 0),
                        backgroundColor: '#2980b9'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    indexAxis: 'y',
                    plugins: { legend: { display: false } },
                    scales: { x: { beginAtZero: true } }
                }
            });
        }

        if (spaceCanvasRef.current) {
            spaceChartRef.current = new Chart(spaceCanvasRef.current, {
                type: 'bar',
                data: {
                    labels,
                    datasets: [{
                        label: 'Средняя экономия места (%)',
                        data: chainStats.map(s => s.avgOverallSpaceSavedPercent || 0),
                        backgroundColor: '#27ae60'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    indexAxis: 'y',
                    plugins: { legend: { display: false } },
                    scales: { x: { beginAtZero: true, max: 100 } }
                }
            });
        }

        return () => {
            if (timeChartRef.current) {
                timeChartRef.current.destroy();
                timeChartRef.current = null;
            }
            if (spaceChartRef.current) {
                spaceChartRef.current.destroy();
                spaceChartRef.current = null;
            }
        };
    }, [chainStats, availableAlgorithms]);

    return (
        <div>
            <h1>Сравнение результатов сжатия</h1>

            {history.length === 0 ? (
                <p style={{ color: '#888' }}>История пуста. Сначала выполните сжатие на странице «Кодирование».</p>
            ) : (
                <>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                        <strong>Записи ({history.length}), выбрано: {selectedIds.length}</strong>
                        <div>
                            <button className="btn-primary" onClick={selectAll} style={{ marginRight: '10px' }}>Выбрать все</button>
                            <button
                                onClick={clearSelection}
                                style={{ color: '#c0392b', border: '1px solid #c0392b', background: '#fff', borderRadius: '4px', padding: '6px 12px', cursor: 'pointer' }}
                            >
                                Очистить выбор
                            </button>
                        </div>
                    </div>

                    <table border="1" cellPadding="8" style={{ width: '100%', borderCollapse: 'collapse', marginBottom: '30px', background: '#fff' }}>
                        <thead>
                            <tr style={{ background: '#f2f2f2' }}>
                                <th></th>
                                <th>Файл</th>
                                <th>Цепочка</th>
                                <th>Дата</th>
                            </tr>
                        </thead>
                        <tbody>
                            {history.map(entry => (
                                <tr key={entry.id}>
                                    <td style={{ textAlign: 'center' }}>
                                        <input
                                            type="checkbox"
                                            checked={selectedIds.includes(entry.id)}
                                            onChange={() => toggleSelect(entry.id)}
                                        />
                                    </td>
                                    <td>{entry.originalName}</td>
                                    <td>{chainDisplayName(entry.chain, availableAlgorithms)}</td>
                                    <td>{formatDate(entry.timestamp)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </>
            )}

            {chainStats.length === 0 ? (
                selectedIds.length > 0 && <p style={{ color: '#888' }}>Нет данных для сравнения.</p>
            ) : (
                <>
                    <h2>Сравнение по цепочкам</h2>

                    {chainStats.map(stat => (
                        <div key={stat.key} style={{ marginBottom: '25px', background: 'white', padding: '15px', borderRadius: '5px', border: '1px solid #ddd' }}>
                            <h4>{chainDisplayName(stat.chain, availableAlgorithms) || '(пустая цепочка)'} — {stat.sampleCount} {stat.sampleCount === 1 ? 'запись' : 'записей'}</h4>
                            <p>
                                Суммарное среднее время: <strong>{formatNumber(stat.totalAvgElapsedMs)} мс</strong>
                                {' | '}
                                Средний коэффициент сжатия (по цепочке целиком): <strong>{formatNumber(stat.avgOverallCompressionRatio, 3)}</strong>
                                {' | '}
                                Средняя экономия (по цепочке целиком): <strong>{formatNumber(stat.avgOverallSpaceSavedPercent, 1)}%</strong>
                            </p>

                            <table border="1" cellPadding="8" style={{ width: '100%', borderCollapse: 'collapse' }}>
                                <thead>
                                    <tr style={{ background: '#f2f2f2' }}>
                                        <th>Шаг</th>
                                        <th>Время (мс)</th>
                                        <th>Скорость (MB/s)</th>
                                        <th>Коэфф. шага</th>
                                        <th>Экономия шага (%)</th>
                                        <th>Память</th>
                                        <th>GC Gen 0</th>
                                        <th>GC Gen 1</th>
                                        <th>GC Gen 2</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {stat.steps.map((s, i) => (
                                        <tr key={i}>
                                            <td>{getStepDisplayName(s.stepName)}</td>
                                            <td>{formatNumber(s.avgElapsedMilliseconds)}</td>
                                            <td>{formatNumber(s.avgThroughputMBps)}</td>
                                            <td>{formatNumber(s.avgCompressionRatio, 3)}</td>
                                            <td>{formatNumber(s.avgSpaceSavedPercent, 1)}%</td>
                                            <td>{formatBytes(s.avgAllocatedBytes)}</td>
                                            <td>{formatNumber(s.avgGen0, 1)}</td>
                                            <td>{formatNumber(s.avgGen1, 1)}</td>
                                            <td>{formatNumber(s.avgGen2, 1)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    ))}

                    <h2>Графики</h2>

                    <h5>Суммарное среднее время выполнения (мс)</h5>
                    <div style={{ background: 'white', padding: '15px', borderRadius: '5px', border: '1px solid #ddd', marginBottom: '20px', position: 'relative', height: '300px' }}>
                        <canvas ref={timeCanvasRef}></canvas>
                    </div>

                    <h5>Средняя экономия места (%)</h5>
                    <div style={{ background: 'white', padding: '15px', borderRadius: '5px', border: '1px solid #ddd', position: 'relative', height: '300px' }}>
                        <canvas ref={spaceCanvasRef}></canvas>
                    </div>
                </>
            )}
        </div>
    );
}