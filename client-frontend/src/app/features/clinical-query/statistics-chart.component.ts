import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ChartConfiguration, ChartData, Plugin } from 'chart.js';
import { QueryResult } from '../../shared/models/clinical-measurement.model';

/** Custom per-dataset properties consumed by the error-bar plugin. */
interface SigmaDatasetProps {
  stdDevs?: (number | null)[];
  whiskerColor?: string;
}

/**
 * Draws ±1σ error-bar whiskers centered on each bar:
 * a vertical line from (value − σ) to (value + σ) with horizontal caps.
 */
const errorBarsPlugin: Plugin<'bar'> = {
  id: 'errorBars',
  afterDatasetsDraw(chart) {
    const yScale = chart.scales['y'];
    if (!yScale) return;

    chart.data.datasets.forEach((dataset, datasetIndex) => {
      const { stdDevs, whiskerColor } = dataset as SigmaDatasetProps;
      if (!stdDevs) return;

      const meta = chart.getDatasetMeta(datasetIndex);
      if (meta.hidden) return;

      meta.data.forEach((element, index) => {
        const value = dataset.data[index];
        const sd = stdDevs[index];
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

/**
 * Bar chart comparing HE and plaintext query results.
 * Bars show the average; whiskers overlay the ±1σ range.
 */
@Component({
  selector: 'app-statistics-chart',
  imports: [BaseChartDirective],
  // Registered here rather than in app.config so Chart.js stays in this lazy chunk.
  providers: [provideCharts(withDefaultRegisterables())],
  template: `
    <div class="chart-wrapper">
      <canvas
        baseChart
        type="bar"
        role="img"
        [attr.aria-label]="ariaLabel()"
        [data]="chartData()"
        [options]="chartOptions()"
        [plugins]="chartPlugins"
      ></canvas>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-height: 0;
    }

    .chart-wrapper {
      position: relative;
      height: 20rem;
      margin-top: 1.5rem;
    }

    /* In the side-by-side layout the chart absorbs the card's extra height. */
    @media (min-width: 960px) {
      .chart-wrapper {
        flex: 1;
        height: auto;
        min-height: 20rem;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatisticsChartComponent {
  readonly heResults = input<QueryResult[] | null>(null);
  readonly plaintextResults = input<QueryResult[] | null>(null);

  readonly chartPlugins = [errorBarsPlugin];

  private readonly labels = computed<string[]>(() => {
    const names = [
      ...(this.heResults() ?? []).map((r) => r.measurementName),
      ...(this.plaintextResults() ?? []).map((r) => r.measurementName),
    ];
    return [...new Set(names)];
  });

  readonly chartData = computed<ChartData<'bar', (number | null)[]>>(() => {
    const labels = this.labels();
    const datasets: ChartData<'bar', (number | null)[]>['datasets'] = [];

    const he = this.heResults();
    if (he?.length) {
      datasets.push({
        label: 'HE (±1σ)',
        data: labels.map((l) => he.find((r) => r.measurementName === l)?.value ?? null),
        backgroundColor: 'rgba(37, 99, 235, 0.75)',
        stdDevs: labels.map((l) => he.find((r) => r.measurementName === l)?.stdDev ?? null),
        whiskerColor: '#1e3a8a',
      } as ChartData<'bar', (number | null)[]>['datasets'][number]);
    }

    const pt = this.plaintextResults();
    if (pt?.length) {
      datasets.push({
        label: 'Plaintext (±1σ)',
        data: labels.map((l) => pt.find((r) => r.measurementName === l)?.value ?? null),
        backgroundColor: 'rgba(22, 163, 74, 0.75)',
        stdDevs: labels.map((l) => pt.find((r) => r.measurementName === l)?.stdDev ?? null),
        whiskerColor: '#14532d',
      } as ChartData<'bar', (number | null)[]>['datasets'][number]);
    }

    return { labels, datasets };
  });

  /** Leaves headroom above the tallest whisker so it isn't clipped by the axis. */
  private readonly suggestedMax = computed<number | undefined>(() => {
    const all = [...(this.heResults() ?? []), ...(this.plaintextResults() ?? [])];
    if (!all.length) return undefined;
    return Math.max(...all.map((r) => r.value + r.stdDev)) * 1.1;
  });

  readonly chartOptions = computed<ChartConfiguration<'bar'>['options']>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      y: {
        beginAtZero: true,
        suggestedMax: this.suggestedMax(),
        title: { display: true, text: 'Value' },
      },
    },
    plugins: {
      legend: { position: 'bottom' },
      tooltip: {
        callbacks: {
          label: (ctx) => {
            const value = ctx.parsed.y;
            if (value == null) return ctx.dataset.label ?? '';
            const sd = (ctx.dataset as SigmaDatasetProps).stdDevs?.[ctx.dataIndex];
            return sd != null
              ? `${ctx.dataset.label}: ${value.toFixed(2)} ± ${sd.toFixed(2)}`
              : `${ctx.dataset.label}: ${value.toFixed(2)}`;
          },
        },
      },
    },
  }));

  readonly ariaLabel = computed<string>(() => {
    const parts: string[] = [];
    for (const [source, results] of [
      ['HE', this.heResults()],
      ['Plaintext', this.plaintextResults()],
    ] as const) {
      for (const r of results ?? []) {
        parts.push(
          `${r.measurementName} (${source}): average ${r.value.toFixed(2)}, standard deviation ${r.stdDev.toFixed(2)} ${r.unitOfMeasurement}`,
        );
      }
    }
    return parts.length
      ? `Bar chart of query results with standard deviation whiskers. ${parts.join('. ')}.`
      : 'Bar chart of query results (no data).';
  });
}
