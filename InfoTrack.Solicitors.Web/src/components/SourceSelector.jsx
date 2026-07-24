import React from "react";

const SourceSelector = ({ sourceName, setSourceName, sourceUrl, setSourceUrl, onApply }) => {
  return (
    <section className="source">
      <h2>Source</h2>
      <div className="source-controls">
        <label>
          Choose:
          <select
            value={sourceName}
            onChange={(e) => {
              const v = e.target.value;
              setSourceName(v);
              if (v === "CLC") {
                setSourceUrl(
                  "https://www.clc-uk.org/wp-content/uploads/2026/06/List-of-CLC-regulated-practices-as-of-04.06.2026.xlsx"
                );
              } else if (v === "InfoTrack") {
                setSourceUrl("");
              }
            }}
          >
            <option value="InfoTrack">InfoTrack (default)</option>
            <option value="CLC">CLC (provided)</option>
            <option value="Custom">Custom</option>
          </select>
        </label>
        <label>
          Source URL:
          <input
            type="text"
            placeholder="Optional source URL (e.g. xlsx)"
            value={sourceUrl}
            onChange={(e) => setSourceUrl(e.target.value)}
          />
        </label>
        <button onClick={onApply}>Apply Source</button>
      </div>
    </section>
  );
};

export default SourceSelector;
