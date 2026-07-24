import React from "react";

const SavedSearchesPanel = ({ savedSearches, loadSavedSearches, onSave, showSaveDialog, setShowSaveDialog, saveName, setSaveName }) => {
  return (
    <section className="saved-searches">
      <h2>Saved Searches</h2>
      <div className="saved-controls">
        <button onClick={loadSavedSearches}>Reload</button>
      </div>

      <ul>
        {savedSearches.map((s) => (
          <li key={s.id ?? s.name}>{s.name}</li>
        ))}
      </ul>

      {showSaveDialog && (
        <div className="save-dialog">
          <label>
            Save as:
            <input type="text" value={saveName} onChange={(e) => setSaveName(e.target.value)} />
          </label>
          <button onClick={() => onSave(saveName)}>Save</button>
          <button onClick={() => setShowSaveDialog(false)}>Cancel</button>
        </div>
      )}
    </section>
  );
};

export default SavedSearchesPanel;
