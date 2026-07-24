import React from "react";

const Pagination = ({ page, pageSize, totalCount, onPrevious, onNext }) => {
  const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
  return (
    <div className="pagination">
      <button onClick={onPrevious} disabled={page <= 1}>Previous</button>
      <span> Page {page} of {maxPage} </span>
      <button onClick={onNext} disabled={page >= maxPage}>Next</button>
    </div>
  );
};

export default Pagination;
