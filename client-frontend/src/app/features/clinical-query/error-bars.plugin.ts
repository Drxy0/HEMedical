import { Plugin } from 'chart.js';

/** Custom per-dataset properties consumed by the error-bar plugin. */
export interface SigmaDatasetProps {
  standardDeviations?: (number | null)[];
  whiskerColor?: string;
}

/**
 * Draws ±1σ error-bar whiskers centered on each bar:
 * a vertical line from (value − σ) to (value + σ) with horizontal caps.
 * Bars whose standard deviation is null are skipped (no whisker).
 */
export const errorBarsPlugin: Plugin<'bar'> = {
  id: 'errorBars',
  afterDatasetsDraw(chart) {
    const yScale = chart.scales['y'];
    if (!yScale) return;

    chart.data.datasets.forEach((dataset, datasetIndex) => {
      const { standardDeviations, whiskerColor } = dataset as SigmaDatasetProps;
      if (!standardDeviations) return;

      const meta = chart.getDatasetMeta(datasetIndex);
      if (meta.hidden) return;

      meta.data.forEach((element, index) => {
        const value = dataset.data[index];
        const sd = standardDeviations[index];
        if (typeof value !== 'number' || sd == null) return;

        const { x, width } = element.getProps(['x', 'width'], true) as {
          x: number;
          width: number;
        };
        const yTop = yScale.getPixelForValue(value + sd);
        const yBottom = yScale.getPixelForValue(value - sd);
        const cap = Math.min(12, width * 0.5);

        const ctx = chart.ctx;
        ctx.save();
        ctx.strokeStyle = whiskerColor ?? '#111827';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(x, yTop);
        ctx.lineTo(x, yBottom);
        ctx.moveTo(x - cap / 2, yTop);
        ctx.lineTo(x + cap / 2, yTop);
        ctx.moveTo(x - cap / 2, yBottom);
        ctx.lineTo(x + cap / 2, yBottom);
        ctx.stroke();
        ctx.restore();
      });
    });
  },
};
