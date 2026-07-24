import React from "react";

const ComparisonPanel = ({ compareMode, setCompareMode, compareId1, setCompareId1, compareId2, setCompareId2, onCompare, comparisonResult, comparisonError }) => {
  return (
    <section className="comparison">
      <h2>Comparison</h2>
      <label>
        <input type="checkbox" checked={compareMode} onChange={(e) => setCompareMode(e.target.checked)} /> Enable Compare
      </label>

      {compareMode && (
        <div className="compare-controls">
          <label>
            ID 1:
            <input value={compareId1} onChange={(e) => setCompareId1(e.target.value)} />
          </label>
          <label>
            ID 2:
            <input value={compareId2} onChange={(e) => setCompareId2(e.target.value)} />
          </label>
          <button onClick={() => onCompare(compareId1, compareId2)}>Compare</button>
        </div>
      )}

      {comparisonError && <p className="error">Error: {comparisonError}</p>}

      {comparisonResult && (
        <div className="comparison-results">
          <h4>Added ({comparisonResult.added.length})</h4>
          <h4>Removed ({comparisonResult.removed.length})</h4>
          <h4>Unchanged ({comparisonResult.unchanged.length})</h4>
        </div>
      )}
    </section>
  );
};

export default ComparisonPanel;
