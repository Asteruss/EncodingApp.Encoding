import { BrowserRouter, Routes, Route, NavLink, Outlet, Navigate } from 'react-router-dom';
import './App.css';
import EncodePage from './pages/EncodePage';
import DecodePage from './pages/DecodePage';
import ComparePage from './pages/ComparePage';
import InfoPage from './pages/InfoPage';
function App() {
    return (
        <BrowserRouter>
            <div className="app-container">
                {/* Боковое меню */}
                <nav className="sidebar">
                    <h2>Сожми его</h2>
                    <ul>
                        <li><NavLink to="/encode" className={({ isActive }) => isActive ? "active" : ""}>Кодирование</NavLink></li>
                        <li><NavLink to="/decode">Декодирование</NavLink></li>
                        <li><NavLink to="/compare">Сравнение</NavLink></li>
                        <li><NavLink to="/info">Справка</NavLink></li>
                    </ul>
                </nav>

                {/* Основной контент страниц */}
                <main className="content">
                    <Routes>
                        <Route path="/" element={<Navigate to="/encode" replace />} />
                        <Route path="/encode" element={<EncodePage />} />
                        <Route path="/decode" element={<DecodePage />} />
                        <Route path="/compare" element={<ComparePage />} />
                        <Route path="/info" element={<InfoPage />} />
                    </Routes>
                    <Outlet />
                </main>
            </div>
        </BrowserRouter>
    );
}

export default App;