import React from "react";

const InsightsPanel = ({ insights, insightsLoading, insightsError, topCount, setTopCount, onFetchInsights }) => {
  return (
    <section className="insights">
      <h2>Insights</h2>
      <div className="insights-controls">
        <label>
          Top count:
          <input type="number" min={1} value={topCount} onChange={(e) => setTopCount(Number(e.target.value || 0))} />
        </label>
        <button onClick={() => onFetchInsights(topCount)}>Refresh Insights</button>
      </div>

      {insightsLoading ? (
        <p>Loading insights...</p>
      ) : insightsError ? (
        <p>Error: {insightsError}</p>
      ) : (
        <ul>
          {insights.map((it, idx) => (
            <li key={`${it.name}-${idx}`}>{it.name} ({it.count})</li>
          ))}
        </ul>
      )}
    </section>
  );
};

export default InsightsPanel;
