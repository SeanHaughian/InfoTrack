import { useEffect, useState } from "react";
import "./App.css";
import apiFetch from "./lib/apiClient";
import { getSelectedLocations, buildExportPath, downloadResponseAsFile } from "./lib/helpers";
import SourceSelector from "./components/SourceSelector";
import LocationsPanel from "./components/LocationsPanel";
import InsightsPanel from "./components/InsightsPanel";
import SavedSearchesPanel from "./components/SavedSearchesPanel";
import ComparisonPanel from "./components/ComparisonPanel";
import ResultsTable from "./components/ResultsTable";
import Pagination from "./components/Pagination";

const defaultLocations = [
    "London",
    "Birmingham",
    "Leeds",
    "Manchester",
    "Sheffield",
    "Bradford",
    "Liverpool",
    "Bristol",
];

function App() {
    const [results, setResults] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(50);
    const [totalCount, setTotalCount] = useState(0);

    // Locations state: array of { id?: number, name: string, checked: boolean }
    const [locations, setLocations] = useState([]);
    const [newLocation, setNewLocation] = useState("");

    // Saved searches state
    const [savedSearches, setSavedSearches] = useState([]);
    const [saveName, setSaveName] = useState("");
    const [showSaveDialog, setShowSaveDialog] = useState(false);

    // Comparison state
    const [compareMode, setCompareMode] = useState(false);
    const [compareId1, setCompareId1] = useState("");
    const [compareId2, setCompareId2] = useState("");
    const [comparisonResult, setComparisonResult] = useState(null);
    const [comparisonError, setComparisonError] = useState(null);

    // Insights state
    const [insights, setInsights] = useState([]);
    const [insightsLoading, setInsightsLoading] = useState(false);
    const [insightsError, setInsightsError] = useState(null);
    const [topCount, setTopCount] = useState(10);
    const [sourceName, setSourceName] = useState("InfoTrack");
    const [sourceUrl, setSourceUrl] = useState("");

    // apiFetch is provided by src/lib/apiClient

    const fetchSolicitors = (selectedLocations, requestedPage = 1, requestedPageSize = pageSize) => {
        setLoading(true);
        setError(null);
        const basePath = '/api/solicitors';
        let path = basePath;
        if (selectedLocations && selectedLocations.length > 0) {
            const encoded = selectedLocations.map((s) => encodeURIComponent(s)).join(",");
            path = `${basePath}?locations=${encoded}`;
        }

        // add paging parameters
        const sep = path.includes("?") ? "&" : "?";
        path = `${path}${sep}page=${requestedPage}&pageSize=${requestedPageSize}`;

        if (sourceName) {
            path += `&sourceName=${encodeURIComponent(sourceName)}`;
        }
        if (sourceUrl) {
            path += `&sourceUrl=${encodeURIComponent(sourceUrl)}`;
        }

        apiFetch(path)
            .then((response) => response.json())
            .then((data) => {
                setResults(data.items || []);
                setTotalCount(data.totalCount ?? 0);
                setPage(data.page ?? requestedPage);
                setPageSize(data.pageSize ?? requestedPageSize);
            })
            .catch((error) => setError(error?.message ?? String(error)))
            .finally(() => setLoading(false));
    };

    const handleRemoveSolicitor = async (sol, index) => {
        // optimistic UI remove
        setResults((prev) => prev.filter((_, i) => i !== index));

        const selectedArr = getSelectedLocations(locations);
        const selectedStr = selectedArr.join(",");

        try {
            await apiFetch('/api/solicitors', {
                method: 'DELETE',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: sol.name, location: sol.location, website: sol.website, locations: selectedStr }),
            });

            // Refresh results & counts from server so totalCount and paging stay correct
            fetchSolicitors(selectedArr, page, pageSize);
        } catch (err) {
            // restore on error
            setResults((prev) => {
                const copy = prev.slice();
                copy.splice(index, 0, sol);
                return copy;
            });
        }
    };

    // Load persisted locations from API on mount, then fetch solicitors for selected locations.
    useEffect(() => {
        const load = async () => {
            try {
                const res = await apiFetch('/api/locations');
                const data = await res.json();
                setLocations(
                    data.map((l) => ({ id: l.id, name: l.name, checked: l.checked }))
                );

                const selected = getSelectedLocations(data);
                // Defer to avoid synchronous state updates in effect
                setTimeout(() => {
                    fetchSolicitors(selected, 1, pageSize);
                    fetchInsights(topCount, selected);
                }, 0);
            } catch (err) {
                // fallback to defaults if API fails
                const restored = defaultLocations.map((name) => ({ name, checked: true }));
                setLocations(restored);
                const selected = getSelectedLocations(restored);
                setTimeout(() => {
                    fetchSolicitors(selected, 1, pageSize);
                    fetchInsights(topCount, selected);
                }, 0);
            }
        };

        load();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const toggleLocation = async (index) => {
        const loc = locations[index];
        if (!loc) return;

        // compute new locations array for optimistic update
        const newLocations = locations.map((l, i) => (i === index ? { ...l, checked: !l.checked } : l));
        setLocations(newLocations);

        // update insights and results optimistically
        const selectedNames = getSelectedLocations(newLocations);
        setPage(1);
        fetchSolicitors(selectedNames, 1, pageSize);
        fetchInsights(topCount, selectedNames);

        // persist if we have an id
        if (loc.id) {
            try {
                await apiFetch(`/api/locations/${loc.id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: loc.id, name: loc.name, checked: !loc.checked }),
                });
            } catch (err) {
                // rollback on error
                setLocations((prev) => prev.map((l, i) => (i === index ? { ...l, checked: loc.checked } : l)));
                // restore previous results/insights
                const restored = locations.filter((l, i) => i !== index && l.checked).map((l) => l.name);
                setTimeout(() => {
                    fetchSolicitors(restored, 1, pageSize);
                    fetchInsights(topCount, restored);
                }, 0);
            }
        }
    };

    const addLocation = async () => {
        const trimmed = newLocation.trim();
        if (!trimmed) return;

        try {
            const res = await apiFetch('/api/locations', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: trimmed }),
            });
            const created = await res.json();
            const newLocs = [...locations, { id: created.id, name: created.name, checked: created.checked }];
            setLocations(newLocs);
            setNewLocation('');
            // refresh insights to reflect new selection set
            const selected = getSelectedLocations(newLocs);
            fetchInsights(topCount, selected);
        } catch (err) {
            // fallback to local-only add
            const newLocs = [...locations, { name: trimmed, checked: true }];
            setLocations(newLocs);
            setNewLocation('');
            const selected = getSelectedLocations(newLocs);
            fetchInsights(topCount, selected);
        }
    };

    const removeLocation = async (index) => {
        const loc = locations[index];
        if (!loc) return;

        // optimistic UI
        const newLocs = locations.filter((_, i) => i !== index);
        setLocations(newLocs);
        const selectedAfterRemove = getSelectedLocations(newLocs);
        fetchInsights(topCount, selectedAfterRemove);

        if (loc.id) {
            try {
                await apiFetch(`/api/locations/${loc.id}`, { method: 'DELETE' });
            } catch (err) {
                // restore on error
                setLocations((prev) => {
                    const copy = prev.slice();
                    copy.splice(index, 0, loc);
                    return copy;
                });
            }
        }
    };

    // Saved searches functions
    const loadSavedSearches = async () => {
        try {
            const res = await apiFetch('/api/savedsearches');
            const data = await res.json();
            setSavedSearches(data);
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Failed to load saved searches:', err);
        }
    };

    const saveCurrentSearch = async () => {
        const trimmed = saveName.trim();
        if (!trimmed) return;

        const selectedLocations = getSelectedLocations(locations);

        try {
            // Fetch ALL results (not just current page) for saving
            const basePath = '/api/solicitors';
            let path = basePath;
            if (selectedLocations && selectedLocations.length > 0) {
                const encoded = selectedLocations.map((s) => encodeURIComponent(s)).join(",");
                path = `${basePath}?locations=${encoded}`;
            }
            // Request a large page size to get all results at once
            const sep = path.includes("?") ? "&" : "?";
            path = `${path}${sep}page=1&pageSize=10000`;

            // eslint-disable-next-line no-console
            console.log('?? Fetching all results for save from:', path);

            const res = await apiFetch(path);
            const data = await res.json();
            const allResults = data.items || [];

            // eslint-disable-next-line no-console
            console.log(`? Fetched ${allResults.length} results from API`);
            // eslint-disable-next-line no-console
            console.log('?? Sample of first 5 results:', allResults.slice(0, 5).map(r => ({
                name: r.name,
                location: r.location,
                phone: r.phone
            })));

            // Now save with all results
            await apiFetch('/api/savedsearches', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: trimmed,
                    locations: selectedLocations,
                    results: allResults
                })
            });

            // eslint-disable-next-line no-console
            console.log(`?? Saved "${trimmed}" with ${allResults.length} results`);

            setSaveName('');
            setShowSaveDialog(false);
            await loadSavedSearches();
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Failed to save search:', err);
        }
    };

    const loadSavedSearch = async (id) => {
        try {
            // eslint-disable-next-line no-console
            console.log(`?? Loading saved search #${id}...`);

            const res = await apiFetch(`/api/savedsearches/${id}`);
            const data = await res.json();

            // eslint-disable-next-line no-console
            console.log(`? Loaded "${data.name}" with ${data.results.length} results`);
            // eslint-disable-next-line no-console
            console.log('?? Sample of first 5 loaded results:', data.results.slice(0, 5).map(r => ({
                name: r.name,
                location: r.location,
                phone: r.phone
            })));

            setResults(data.results);
            setTotalCount(data.results.length);
            setPage(1);

            // Update locations to match the saved search
            const savedLocations = data.locations;
            setLocations((prev) => prev.map((loc) => ({
                ...loc,
                checked: savedLocations.includes(loc.name)
            })));
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Failed to load saved search:', err);
        }
    };

    const deleteSavedSearch = async (id) => {
        try {
            await apiFetch(`/api/savedsearches/${id}`, { method: 'DELETE' });
            await loadSavedSearches();
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Failed to delete saved search:', err);
        }
    };

    const compareSearches = async () => {
        if (!compareId1 || !compareId2) return;

        setComparisonError(null);
        setComparisonResult(null);

        try {
            // eslint-disable-next-line no-console
            console.log(`?? Comparing search #${compareId1} vs #${compareId2}...`);

            const res = await apiFetch('/api/savedsearches/compare', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    id1: parseInt(compareId1),
                    id2: parseInt(compareId2)
                })
            });

            if (!res.ok) {
                const error = await res.json();
                setComparisonError(error.error || 'Comparison failed');
                // eslint-disable-next-line no-console
                console.error('? Comparison failed:', error);
                return;
            }

            const data = await res.json();

            // eslint-disable-next-line no-console
            console.log('?? Comparison result:');
            // eslint-disable-next-line no-console
            console.log(`  Search 1: "${data.search1.name}" - ${data.search1.results.length} results`);
            // eslint-disable-next-line no-console
            console.log(`  Search 2: "${data.search2.name}" - ${data.search2.results.length} results`);
            // eslint-disable-next-line no-console
            console.log(`  Added: ${data.added.length}`);
            // eslint-disable-next-line no-console
            console.log(`  Removed: ${data.removed.length}`);
            // eslint-disable-next-line no-console
            console.log(`  Unchanged: ${data.unchanged.length}`);

            if (data.added.length > 0) {
                // eslint-disable-next-line no-console
                console.log('? Added firms:', data.added.slice(0, 3).map(r => r.name));
            }
            if (data.removed.length > 0) {
                // eslint-disable-next-line no-console
                console.log('? Removed firms:', data.removed.slice(0, 3).map(r => r.name));
            }

            setComparisonResult(data);
        } catch (err) {
            setComparisonError(err?.message ?? String(err));
            // eslint-disable-next-line no-console
            console.error('? Comparison error:', err);
        }
    };

    const fetchInsights = async (count = topCount, selected = null) => {
        setInsightsLoading(true);
        setInsightsError(null);

        try {
            let path = `/api/solicitors/insights?top=${count}`;
            const selectedList = selected ?? getSelectedLocations(locations);
            if (selectedList && selectedList.length > 0) {
                const encoded = selectedList.map((s) => encodeURIComponent(s)).join(',');
                path += `&locations=${encoded}`;
            }

            if (sourceName) {
                path += `&sourceName=${encodeURIComponent(sourceName)}`;
            }
            if (sourceUrl) {
                path += `&sourceUrl=${encodeURIComponent(sourceUrl)}`;
            }

            const res = await apiFetch(path);
            const data = await res.json();
            setInsights(data);
        } catch (err) {
            setInsightsError(err?.message ?? String(err));
        } finally {
            setInsightsLoading(false);
        }
    };

    // Load saved searches on mount
    useEffect(() => {
        loadSavedSearches();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    // Load insights on mount
    useEffect(() => {
        fetchInsights();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    if (loading) {
        return <p>Loading solicitors...</p>;
    }

    if (error) {
        return <p>Error: {error}</p>;
    }

    return (
        <main>
            <h1>Conveyancing Solicitors Report</h1>

            <SourceSelector
                sourceName={sourceName}
                setSourceName={setSourceName}
                sourceUrl={sourceUrl}
                setSourceUrl={setSourceUrl}
                onApply={() => {
                    const selected = getSelectedLocations(locations);
                    setPage(1);
                    fetchSolicitors(selected, 1, pageSize);
                    fetchInsights(topCount, selected);
                }}
            />

            <LocationsPanel
                sourceName={sourceName}
                locations={locations}
                newLocation={newLocation}
                setNewLocation={setNewLocation}
                onAddLocation={addLocation}
                onToggleLocation={toggleLocation}
                onRemoveLocation={removeLocation}
                onFetchResults={() => {
                    const selected = getSelectedLocations(locations);
                    setPage(1);
                    fetchSolicitors(selected, 1, pageSize);
                }}
                onRestoreDefaults={async () => {
                    try {
                        const res = await apiFetch('/api/locations/restore', { method: 'POST' });
                        const data = await res.json();
                        setLocations(data.map((l) => ({ id: l.id, name: l.name, checked: l.checked })));
                        setPage(1);
                        const selected = getSelectedLocations(data);
                        fetchSolicitors(selected, 1, pageSize);
                    } catch (err) {
                        // fallback: client-side restore
                        const restored = defaultLocations.map((name) => ({ name, checked: true }));
                        setLocations(restored);
                        setPage(1);
                        fetchSolicitors(defaultLocations, 1, pageSize);
                    }
                }}
                onSaveCurrentResults={() => setShowSaveDialog(true)}
                onExportJson={async () => {
                    // Export current filtered results as JSON
                    const selected = getSelectedLocations(locations);
                    let path = '/api/solicitors/export/json';
                    const params = new URLSearchParams();
                    if (selected && selected.length > 0) params.set('locations', selected.map(s => encodeURIComponent(s)).join(','));
                    if (sourceName) params.set('sourceName', sourceName);
                    if (sourceUrl) params.set('sourceUrl', sourceUrl);
                    if ([...params].length > 0) path += `?${params.toString()}`;

                    try {
                        const res = await apiFetch(path);
                        const blob = await res.blob();
                        const url = URL.createObjectURL(blob);
                        const a = document.createElement('a');
                        a.href = url;
                        a.download = `solicitors-${new Date().toISOString().replace(/[:.]/g, '-')}.json`;
                        document.body.appendChild(a);
                        a.click();
                        a.remove();
                        URL.revokeObjectURL(url);
                    } catch (err) {
                        // eslint-disable-next-line no-console
                        console.error('Export JSON failed', err);
                    }
                }}
                onExportExcel={async () => {
                    // Export current filtered results as Excel (.xlsx)
                    {
                        const selected = getSelectedLocations(locations);
                        const path = buildExportPath('/api/solicitors/export/excel', selected, sourceName, sourceUrl);
                        try {
                            const res = await apiFetch(path);
                            await downloadResponseAsFile(res, `solicitors-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`);
                        } catch (err) {
                            // eslint-disable-next-line no-console
                            console.error('Export Excel failed', err);
                        }
                    }
                }}
                disableSave={results.length === 0}
                disableExport={results.length === 0}
            />

            {showSaveDialog && (
                <div className="modal-overlay">
                    <div className="modal">
                        <h3>Save Search Results</h3>
                        <input
                            type="text"
                            placeholder="Enter a name for this search"
                            value={saveName}
                            onChange={(e) => setSaveName(e.target.value)}
                            onKeyPress={(e) => {
                                if (e.key === 'Enter') saveCurrentSearch();
                            }}
                        />
                        <div className="modal-actions">
                            <button onClick={saveCurrentSearch}>Save</button>
                            <button onClick={() => { setShowSaveDialog(false); setSaveName(''); }}>Cancel</button>
                        </div>
                    </div>
                </div>
            )}

            <section className="insights">
                <h2>Top Solicitors by Reviews</h2>
                <div className="insights-controls">
                    <label>
                        Show top:
                        <select 
                            value={topCount} 
                            onChange={(e) => setTopCount(parseInt(e.target.value))}
                        >
                            <option value="5">5</option>
                            <option value="10">10</option>
                            <option value="25">25</option>
                            <option value="50">50</option>
                        </select>
                    </label>
                    <button onClick={() => {
                        const selected = getSelectedLocations(locations);
                        fetchInsights(topCount, selected);
                    }}>Refresh</button>
                </div>

                {insightsLoading && <p>Loading insights...</p>}
                {insightsError && <p className="error-message">Error: {insightsError}</p>}
                {!insightsLoading && !insightsError && insights.length === 0 && (
                    <p>No solicitors found with reviews.</p>
                )}
                {!insightsLoading && !insightsError && insights.length > 0 && (
                    <table className="insights-table">
                        <thead>
                            <tr>
                                <th>Rank</th>
                                <th>Firm</th>
                                <th>Location</th>
                                <th>Reviews</th>
                                <th>Phone</th>
                                <th>Website</th>
                            </tr>
                        </thead>
                        <tbody>
                            {insights.map((solicitor, index) => (
                                <tr key={`${solicitor.name}-${solicitor.location}-${index}`}>
                                    <td>{index + 1}</td>
                                    <td>{solicitor.name}</td>
                                    <td>{solicitor.location}</td>
                                    <td><strong>{solicitor.reviewCount}</strong></td>
                                    <td>{solicitor.phone || 'N/A'}</td>
                                    <td>
                                        {solicitor.website ? (
                                            <a href={solicitor.website} target="_blank" rel="noopener noreferrer">
                                                Visit
                                            </a>
                                        ) : (
                                            'N/A'
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </section>

            <section className="saved-searches">
                <h2>Saved Searches</h2>
                {sourceName === 'CLC' ? (
                    <div className="not-available">
                        <p>Saved searches not available for this source.</p>
                    </div>
                ) : (
                    (savedSearches.length === 0 ? (
                        <p>No saved searches yet. Save your current results to see them here.</p>
                    ) : (
                        <ul className="saved-searches-list">
                            {savedSearches.map((search) => (
                                <li key={search.id}>
                                    <div className="search-info">
                                        <strong>{search.name}</strong>
                                        <span className="search-meta">
                                            {new Date(search.timestamp).toLocaleString()} | 
                                            {' '}{search.results.length} results | 
                                            {' '}Locations: {search.locations.join(', ')}
                                        </span>
                                    </div>
                                    <div className="search-actions">
                                        <button onClick={() => loadSavedSearch(search.id)}>Load</button>
                                        <button className="remove" onClick={() => deleteSavedSearch(search.id)}>Delete</button>
                                    </div>
                                </li>
                            ))}
                        </ul>
                    ))
                )}
            </section>

            <section className="comparison">
                <h2>Compare Searches</h2>
                {sourceName === 'CLC' ? (
                    <div className="not-available">
                        <p>Compare searches not available for this source.</p>
                    </div>
                ) : (
                    <div className="comparison-controls">
                        <div className="compare-select">
                            <label>First Search:</label>
                            <select 
                                value={compareId1} 
                                onChange={(e) => setCompareId1(e.target.value)}
                                disabled={savedSearches.length < 2}
                            >
                                <option value="">Select a saved search</option>
                                {savedSearches.map((search) => (
                                    <option key={search.id} value={search.id}>
                                        {search.name} ({new Date(search.timestamp).toLocaleDateString()})
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div className="compare-select">
                            <label>Second Search:</label>
                            <select 
                                value={compareId2} 
                                onChange={(e) => setCompareId2(e.target.value)}
                                disabled={savedSearches.length < 2}
                            >
                                <option value="">Select a saved search</option>
                                {savedSearches.map((search) => (
                                    <option key={search.id} value={search.id}>
                                        {search.name} ({new Date(search.timestamp).toLocaleDateString()})
                                    </option>
                                ))}
                            </select>
                        </div>
                        <button 
                            onClick={compareSearches}
                            disabled={!compareId1 || !compareId2 || compareId1 === compareId2}
                        >
                            Compare
                        </button>
                    </div>
                )}

                {comparisonError && (
                    <div className="error-message">
                        <strong>Error:</strong> {comparisonError}
                    </div>
                )}

                {comparisonResult && (
                    <div className="comparison-results">
                        <h3>Comparison Results</h3>
                        <div className="comparison-summary">
                            <p><strong>{comparisonResult.search1.name}</strong> vs <strong>{comparisonResult.search2.name}</strong></p>
                            <p>Locations: {comparisonResult.search1.locations.join(', ')}</p>
                        </div>

                        <div className="comparison-section added">
                            <h4>Added ({comparisonResult.added.length})</h4>
                            {comparisonResult.added.length === 0 ? (
                                <p>No new solicitors</p>
                            ) : (
                                <table>
                                    <thead>
                                        <tr>
                                            <th>Firm</th>
                                            <th>Location</th>
                                            <th>Address</th>
                                            <th>Phone</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {comparisonResult.added.map((sol, idx) => (
                                            <tr key={idx}>
                                                <td>{sol.name}</td>
                                                <td>{sol.location}</td>
                                                <td>{sol.address ?? 'N/A'}</td>
                                                <td>{sol.phone ?? 'N/A'}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )}
                        </div>

                        <div className="comparison-section removed">
                            <h4>Removed ({comparisonResult.removed.length})</h4>
                            {comparisonResult.removed.length === 0 ? (
                                <p>No removed solicitors</p>
                            ) : (
                                <table>
                                    <thead>
                                        <tr>
                                            <th>Firm</th>
                                            <th>Location</th>
                                            <th>Address</th>
                                            <th>Phone</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {comparisonResult.removed.map((sol, idx) => (
                                            <tr key={idx}>
                                                <td>{sol.name}</td>
                                                <td>{sol.location}</td>
                                                <td>{sol.address ?? 'N/A'}</td>
                                                <td>{sol.phone ?? 'N/A'}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )}
                        </div>

                        <div className="comparison-section unchanged">
                            <h4>Unchanged ({comparisonResult.unchanged.length})</h4>
                            <p>{comparisonResult.unchanged.length} solicitors remained the same</p>
                        </div>
                    </div>
                )}
            </section>

            <p>Showing {results.length} of {totalCount} results (page {page} of {Math.max(1, Math.ceil(totalCount / pageSize))})</p>

            <Pagination
                page={page}
                pageSize={pageSize}
                totalCount={totalCount}
                onPrevious={() => {
                    const selected = getSelectedLocations(locations);
                    const newPage = Math.max(1, page - 1);
                    fetchSolicitors(selected, newPage, pageSize);
                }}
                onNext={() => {
                    const selected = getSelectedLocations(locations);
                    const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
                    const newPage = Math.min(maxPage, page + 1);
                    fetchSolicitors(selected, newPage, pageSize);
                }}
            />

            <ResultsTable results={results} sourceName={sourceName} onRemove={handleRemoveSolicitor} />
        </main>
    );
}

export default App;
