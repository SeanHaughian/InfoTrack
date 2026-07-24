import React from "react";

const ResultsTable = ({ results, sourceName, onRemove }) => {
  return (
    <table>
      <thead>
        <tr>
          <th className="col-firm">Firm</th>
          <th>Location</th>
          <th>Address</th>
          <th>Phone</th>
          <th>Reviews</th>
          <th className="col-website">Link</th>
          <th className="col-actions">Actions</th>
        </tr>
      </thead>

      <tbody>
        {results.map((solicitor, index) => (
          <tr key={`${solicitor.name}-${index}`}>
            <td>{solicitor.name}</td>
            <td>{solicitor.location}</td>
            <td className="address">{solicitor.address ?? "N/A"}</td>
            <td>{solicitor.phone ?? "N/A"}</td>
            <td>{solicitor.reviewCount ?? "N/A"}</td>
            <td>
              {solicitor.website ? (
                <a className="link-button" href={solicitor.website} target="_blank" rel="noopener noreferrer">Visit</a>
              ) : (
                "N/A"
              )}
            </td>
            <td>
              {sourceName === 'CLC' ? (
                <span className="not-available">Not available for this source</span>
              ) : (
                <button className="remove" onClick={() => onRemove(solicitor, index)}>Remove</button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
};

export default ResultsTable;
