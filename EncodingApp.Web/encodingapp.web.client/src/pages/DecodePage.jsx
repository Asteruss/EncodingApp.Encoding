import { useState, useRef } from 'react';

// Вспомогательная функция для конвертации Base64 в Blob и скачивания
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

export default function DecodePage() {
    const [fileItems, setFileItems] = useState([]); // [{ id, file, name }]
    const [errors, setErrors] = useState({ files: '' });
    const [isLoading, setIsLoading] = useState(false);
    const fileInputRef = useRef(null);

    // 1. Загрузка файлов со строгой валидацией
    const handleFiles = (e) => {
        const selectedFiles = e.target.files || e.dataTransfer.files;
        let hasInvalidFile = false;
        const newItems = [];

        Array.from(selectedFiles).forEach(f => {
            if (!f.name.toLowerCase().endsWith('.cbin')) {
                hasInvalidFile = true;
                return; // Пропускаем неверный файл
            }
            newItems.push({
                id: Date.now() + Math.random(), // Уникальный ID для списка
                file: f,
                name: f.name
            });
        });

        if (hasInvalidFile) {
            setErrors(prev => ({ ...prev, files: "Выбраны файлы с неверным форматом. Разрешены только файлы с расширением .cbin" }));
        } else {
            setErrors(prev => ({ ...prev, files: '' }));
            setFileItems(prev => [...prev, ...newItems]);
        }
    };

    const removeFile = (idToRemove) => {
        setFileItems(prev => prev.filter(item => item.id !== idToRemove));
    };

    // 2. Отправка на сервер
    const handleSubmit = async () => {
        if (fileItems.length === 0) {
            setErrors({ files: "Добавьте хотя бы один .cbin файл." });
            return;
        }

        setIsLoading(true);
        const formData = new FormData();
        fileItems.forEach(item => formData.append("files", item.file));

        try {
            const response = await fetch('/api/compression/decode', { method: 'POST', body: formData });
            if (!response.ok) throw new Error(`Ошибка сервера: HTTP ${response.status}`);

            const data = await response.json();

            setErrors({ files: '' });
            setFileItems([]);
            setTimeout(() => {
                data.forEach(item => {
                    let fileName = item.restoredName || item.originalName.replace('.cbin', '_restored.cbin');
                    downloadFile(item.base64File, fileName);
                });
            }, 150);

        } catch (error) {
            setErrors({ files: error.message });
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div>
            <h1>Декодирование данных</h1>

            {/* Ошибка валидации (над зоной) */}
            {errors.files && <div className="error-box">{errors.files}</div>}

            {/* Зона загрузки */}
            <div
                className="drop-zone"
                onDragOver={(e) => { e.preventDefault(); e.currentTarget.classList.add('dragover'); }}
                onDragLeave={(e) => e.currentTarget.classList.remove('dragover')}
                onDrop={handleFiles}
                onClick={() => fileInputRef.current.click()}
            >
                <input ref={fileInputRef} type="file" multiple accept=".cbin" onChange={handleFiles} style={{ display: 'none' }} />
                Перетащите сюда файлы .cbin или нажмите для выбора
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

            <button className="btn-primary" onClick={handleSubmit} disabled={isLoading}>
                {isLoading ? 'Распаковка...' : 'Распаковать и скачать'}
            </button>
        </div>
    );
}