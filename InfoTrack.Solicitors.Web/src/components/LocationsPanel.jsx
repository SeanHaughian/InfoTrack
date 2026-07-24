import React from "react";

const LocationsPanel = ({
  sourceName,
  locations,
  newLocation,
  setNewLocation,
  onAddLocation,
  onToggleLocation,
  onRemoveLocation,
  onFetchResults,
  onRestoreDefaults,
  onSaveCurrentResults,
  onExportJson,
  onExportExcel,
  disableSave,
  disableExport,
}) => {
  return (
    <section className="locations">
      <h2>Locations</h2>
      {sourceName === "CLC" ? (
        <div className="not-available">
          <p>Locations not available for this source.</p>
        </div>
      ) : (
        <>
          <div className="add-location">
            <input
              type="text"
              placeholder="Add location"
              value={newLocation}
              onChange={(e) => setNewLocation(e.target.value)}
            />
            <button onClick={onAddLocation}>Add</button>
          </div>

          <ul className="locations-list">
            {locations.map((loc, idx) => (
              <li key={`${loc.name}-${idx}`}>
                <label>
                  <input
                    type="checkbox"
                    checked={loc.checked}
                    onChange={() => onToggleLocation(idx)}
                  />
                  {loc.name}
                </label>
                <button className="remove" onClick={() => onRemoveLocation(idx)}>
                  Remove
                </button>
              </li>
            ))}
          </ul>

          <div className="actions">
            <button onClick={onFetchResults}>Fetch Results</button>
            <button onClick={onRestoreDefaults}>Restore Defaults</button>
            <button onClick={onSaveCurrentResults} disabled={disableSave}>
              Save Current Results
            </button>
            <button onClick={onExportJson} disabled={disableExport}>Export JSON</button>
            <button onClick={onExportExcel} disabled={disableExport}>Export Excel</button>
          </div>
        </>
      )}
    </section>
  );
};

export default LocationsPanel;
