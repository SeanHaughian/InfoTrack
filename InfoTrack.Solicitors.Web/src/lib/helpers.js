export const getSelectedLocations = (locations) => {
  if (!locations) return [];
  return locations.filter((l) => l.checked).map((l) => l.name);
};

export const buildExportPath = (basePath, selected, sourceName, sourceUrl) => {
  let path = basePath;
  const params = new URLSearchParams();
  if (selected && selected.length > 0) params.set('locations', selected.map(s => encodeURIComponent(s)).join(','));
  if (sourceName) params.set('sourceName', sourceName);
  if (sourceUrl) params.set('sourceUrl', sourceUrl);
  if ([...params].length > 0) path += `?${params.toString()}`;
  return path;
};

export const downloadResponseAsFile = async (res, filename) => {
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
};

export default {
  getSelectedLocations,
  buildExportPath,
  downloadResponseAsFile,
};
