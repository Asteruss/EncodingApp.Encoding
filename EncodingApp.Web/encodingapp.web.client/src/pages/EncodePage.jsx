import { useState, useEffect, useRef} from 'react';

export default function EncodePage() {
    // Состояния
    const [fileItems, setFileItems] = useState([]);
    const [availableAlgorithms, setAvailableAlgorithms] = useState([]);
    const [rules, setRules] = useState([]);
    const [chain, setChain] = useState([]);
    const [results, setResults] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [errors, setErrors] = useState({ files: '', algorithms: '' });
    const [warnings, setWarnings] = useState([]);
    const fileInputRef = useRef(null);

    // 1. Загрузка алгоритмов и правил при старте
    useEffect(() => {
        fetch('/api/compression/algorithms')
            .then(res => {
                if (!res.ok) throw new Error(`Сервер вернул ошибку: ${res.status}`);
                return res.json();
            })
            .then(data => {
                setAvailableAlgorithms(data.algorithms || []);
                setRules(data.validationRules || []);
            })
            .catch(err => {
                console.error("Ошибка алгоритмов:", err);
                setErrors(prev => ({ ...prev, algorithms: `Не удалось загрузить алгоритмы: ${err.message}` }));
            });
    }, []);

    // 2. Клиентская валидация цепочки (работает при каждом изменении chain)
    useEffect(() => {
        const currentIds = chain.map(a => a.id);
        const newWarnings = [];

        if (currentIds.length > 0) {
            rules.forEach(rule => {
                const isTargetSelected = currentIds.includes(rule.target);
                if (!isTargetSelected) return;

                if (rule.type === 'WarnIfAlone' && currentIds.length === 1) {
                    newWarnings.push(rule.message);
                }
                else if (rule.type === 'RequiresAnyOf') {
                    const hasRequired = rule.requiredIds?.some(r => currentIds.includes(r));
                    if (!hasRequired) newWarnings.push(rule.message);
                }
                else if (rule.type === 'PreferredAfter') {
                    const targetIdx = currentIds.indexOf(rule.target);
                    const hasPreferredBefore = rule.relatedIds?.some(relId => {
                        const relIdx = currentIds.indexOf(relId);
                        return relIdx !== -1 && relIdx > targetIdx;
                    });
                    if (hasPreferredBefore) newWarnings.push(rule.message);
                }
                else if (rule.type === 'IncompatibleWith') {
                    const hasIncompatible = rule.incompatibleIds?.some(incId => currentIds.includes(incId));
                    if (hasIncompatible) newWarnings.push(rule.message);
                }
            });
        }
        setWarnings(newWarnings);
    }, [chain, rules]);

    // 3. Работа с файлами
    const handleFiles = (e) => {
        let selectedFiles = e.target.files || e.dataTransfer.files;
        const newItems = Array.from(selectedFiles).map(f => ({
            id: Date.now() + Math.random(),
            file: f,
            name: f.name
        }));
        setFileItems(prev => [...prev, ...newItems]);
        setErrors(prev => ({ ...prev, files: '' }));
    };

    const removeFile = (idToRemove) => {
        setFileItems(prev => prev.filter(item => item.id !== idToRemove));
    };

    // 4. Логика Drag & Drop алгоритмов
    const handleDragStart = (e, algo, fromList) => {
        e.dataTransfer.setData('text/plain', JSON.stringify({ id: algo.id, from: fromList }));
        e.dataTransfer.effectAllowed = 'move';
    };

    const handleDropOnChain = (e) => {
        e.preventDefault();
        e.currentTarget.classList.remove('dragover');
        const data = JSON.parse(e.dataTransfer.getData('text/plain'));
        const dropTarget = e.target.closest('.chain-chip');
        const dropIndex = dropTarget ? Array.from(e.currentTarget.children).indexOf(dropTarget) : chain.length;

        setChain(prevChain => {
            const newChain = [...prevChain];
            if (data.from === 'pool') {
                const algoToAdd = availableAlgorithms.find(a => a.id === data.id);
                if (algoToAdd) newChain.splice(dropIndex, 0, algoToAdd);
            } else {
                const dragIndex = newChain.findIndex(a => a.id === data.id);
                if (dragIndex === -1) return prevChain;
                const [draggedItem] = newChain.splice(dragIndex, 1);
                newChain.splice(dropIndex, 0, draggedItem);
            }
            return newChain;
        });
    };

    const removeFromChain = (idToRemove) => {
        setChain(prev => prev.filter(item => item.id !== idToRemove));
    };

    const downloadFile = (base64Data, fileName) => {
        const binaryString = atob(base64Data);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: 'application/octet-stream' });
        const url = URL.createObjectURL(blob);

        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        // Отзываем URL с задержкой, чтобы браузер успел начать скачивание
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    };



    const handleSubmit = async () => {
        let currentErrors = { files: '', algorithms: '' };

        if (fileItems.length === 0) currentErrors.files = "Добавьте хотя бы один файл.";
        if (chain.length === 0) currentErrors.algorithms = "Добавьте хотя бы один алгоритм в цепочку.";

        setErrors(currentErrors);
        if (currentErrors.files || currentErrors.algorithms) return;

        setIsLoading(true);
        const formData = new FormData();
        fileItems.forEach(item => formData.append("files", item.file));
        formData.append("algorithms", chain.map(a => a.id).join(','));

        fetch('/api/compression/encode', { method: 'POST', body: formData })
            .then(res => {
                if (!res.ok) throw new Error(`Сервер вернул ошибку: HTTP ${res.status}`);
                return res.json();
            })
            .then(data => {
                console.log(data);
                setResults(data);
                setFileItems([]);
                setErrors({ files: '', algorithms: '' });
                setWarnings([]);

                setTimeout(() => {
                    data.forEach(item => downloadFile(item.base64Cbin, `${item.originalName.replace(/\.[^.]+$/, "")}.cbin`));
                }, 150);


            })
            .catch(error => {
                setErrors(prev => ({ ...prev, algorithms: error.message }));
            })
            .finally(() => {
                setIsLoading(false);
            });
    };

    return (
        <div>
            <h1>Кодирование данных</h1>

            {/* Зона загрузки */}
            <div
                className="drop-zone"
                onDragOver={(e) => { e.preventDefault(); e.currentTarget.classList.add('dragover'); }}
                onDragLeave={(e) => e.currentTarget.classList.remove('dragover')}
                onDrop={handleFiles}
                onClick={() => fileInputRef.current.click()}
            >
                <input ref={fileInputRef} type="file" multiple onChange={handleFiles} style={{ display: 'none' }} />
                Перетащите файлы сюда или нажмите для выбора
            </div>

            {/* Список выбранных файлов */}
            {fileItems.length > 0 && (
                <div style={{ marginBottom: '20px', background: '#fff', padding: '10px', borderRadius: '5px', border: '1px solid #ddd' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                        <strong>Выбранные файлы ({fileItems.length}):</strong>
                        <button
                            onClick={() => setFileItems([])}
                            style={{ color: '#c0392b', border: '1px solid #c0392b', background: '#fff', borderRadius: '4px', padding: '2px 8px', cursor: 'pointer' }}
                        >
                            Удалить все
                        </button>
                    </div>
                    <ul style={{ listStyleType: 'none', padding: 0, margin: 0 }}>
                        {fileItems.map(item => (
                            <li key={item.id} style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                                {item.name}
                                <button onClick={() => removeFile(item.id)} style={{ color: 'red', border: 'none', background: 'none', cursor: 'pointer', fontWeight: 'bold' }}>✕</button>
                            </li>
                        ))}
                    </ul>
                </div>
            )}

            <h3>Доступные алгоритмы (перетащите вниз)</h3>
            <div className="algorithm-pool">
                {availableAlgorithms.length === 0 ? "Загрузка..." :
                    availableAlgorithms.map(algo => (
                        <div
                            key={algo.id}
                            className="algo-chip"
                            draggable
                            onDragStart={(e) => handleDragStart(e, algo, 'pool')}
                        >
                            {algo.displayName}
                        </div>
                    ))
                }
            </div>

            <h3>Цепочка сжатия (порядок важен)</h3>
            <div
                className="chain-box"
                onDragOver={(e) => { e.preventDefault(); e.currentTarget.classList.add('dragover'); }}
                onDragLeave={(e) => e.currentTarget.classList.remove('dragover')}
                onDrop={handleDropOnChain}
            >
                {chain.length === 0 ? <p style={{ color: '#888', margin: 0 }}>Перетащите алгоритмы сюда</p> :
                    chain.map((algo, index) => (
                        <span
                            key={index + 1}
                            className="chain-chip"
                            draggable
                            onDragStart={(e) => handleDragStart(e, algo, 'chain')}
                            title="Нажми, чтобы удалить"
                            onClick={() => removeFromChain(algo.id)}
                            style={{ cursor: 'pointer' }}
                        >
                            {index + 1}. {algo.displayName}
                        </span>
                    ))
                }
            </div>

            {errors.files && <div className="error-box">{errors.files}</div>}
            {errors.algorithms && <div className="error-box">{errors.algorithms}</div>}

            {warnings.length > 0 && (
                <div className="warning-box">
                    <strong>Предупреждения:</strong>
                    <ul style={{ margin: '5px 0 0 0', paddingLeft: '20px' }}>
                        {warnings.map((w, i) => <li key={i}>{w}</li>)}
                    </ul>
                </div>
            )}

            <button className="btn-primary" onClick={handleSubmit} disabled={isLoading}>
                {isLoading ? 'Сжатие...' : 'Сжать и скачать'}
            </button>


            {/* Вывод метрик */}
            {results.length > 0 && results.map((res, index) => {
                const fileMetrics = {
                    timing: res.metrics?.timing || [],
                    compression: res.metrics?.compressionRatio || [],
                    gc: res.metrics?.gcPressure || []
                };

                const formatBytes = (bytes) => {
                    if (bytes === null || bytes === undefined || Number.isNaN(Number(bytes))) return "N/A";
                    const num = Number(bytes);
                    if (Math.abs(num) < 1024) return `${num} Б`;
                    if (Math.abs(num) < 1024 * 1024) return `${(num / 1024).toFixed(1)} КБ`;
                    return `${(num / (1024 * 1024)).toFixed(2)} МБ`;
                };

                const formatNumber = (value, digits = 2) => {
                    if (value === null || value === undefined || Number.isNaN(Number(value))) return "N/A";
                    return Number(value).toFixed(digits);
                };

                return (
                    <div key={index} style={{ marginTop: '20px', background: 'white', padding: '15px', borderRadius: '5px', border: '1px solid #ddd' }}>
                        <h4>Файл: {res.originalName} {results.length > 1 ? `(Файл №${index + 1})` : ''}</h4>

                        <h5>Время и скорость</h5>
                        <table border="1" cellPadding="8" style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <thead>
                                <tr style={{ background: '#f2f2f2' }}><th>Шаг</th><th>Время (мс)</th><th>Скорость (MB/s)</th></tr>
                            </thead>
                            <tbody>
                                {(fileMetrics.timing || []).map((m, i) => (
                                    <tr key={i}>
                                        <td>{m?.stepName ?? "N/A"}</td>
                                        <td>{formatNumber(m?.elapsedMilliseconds)}</td>
                                        <td>{formatNumber(m?.throughputMBps)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>

                        <h5 style={{ marginTop: '15px' }}>Сжатие и Память (GC)</h5>
                        <table border="1" cellPadding="8" style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <thead>
                                <tr style={{ background: '#f2f2f2' }}>
                                    <th>Шаг</th><th>Коэффициент</th><th>Экономия (%)</th>
                                    <th>Выделено памяти</th><th>GC Gen 0</th><th>GC Gen 1</th><th>GC Gen 2</th>
                                </tr>
                            </thead>
                            <tbody>
                                {(fileMetrics.compression || []).map((m, i) => {
                                    const gc = fileMetrics.gc?.[i];
                                    return (
                                        <tr key={i}>
                                            <td>{m?.stepName ?? "N/A"}</td>
                                            <td>{formatNumber(m?.compressionRatio, 3)}</td>
                                            <td>{m?.spaceSavedPercent !== null && m?.spaceSavedPercent !== undefined ? `${formatNumber(m.spaceSavedPercent, 1)}%` : "N/A"}</td>
                                            <td>{formatBytes(gc?.allocatedBytesDelta)}</td>
                                            <td>{gc?.gen0CollectionsDelta ?? "N/A"}</td>
                                            <td>{gc?.gen1CollectionsDelta ?? "N/A"}</td>
                                            <td style={{ color: (gc?.gen2CollectionsDelta ?? 0) > 0 ? 'red' : 'green', fontWeight: 'bold' }}>
                                                {gc?.gen2CollectionsDelta ?? "N/A"}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>

                    </div>
                )
            })}
        </div>
    );
}