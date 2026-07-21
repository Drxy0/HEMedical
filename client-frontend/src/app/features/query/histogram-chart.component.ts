import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { HistogramResult } from '../../shared/models/clinical-measurement.model';

/**
 * Frequency histogram: one bar per value bin, bar height = patient count in that bin.
 * HE in blue and the plaintext verification in green; the counts are integers and the
 * two should match exactly, so overlapping bars double as a visual verification.
 */
@Component({
  selector: 'app-histogram-chart',
  imports: [BaseChartDirective],
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
      ></canvas>
    </div>
    @if (edgeNote(); as note) {
      <p class="edge-note">{{ note }}</p>
    }
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
      height: 22rem;
      margin-top: 1.5rem;
    }

    .edge-note {
      margin: 0.5rem 0 0;
      font-size: 0.8125rem;
      color: #6b7280;
    }

    @media (min-width: 960px) {
      .chart-wrapper {
        flex: 1;
        height: auto;
        min-height: 22rem;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistogramChartComponent {
  readonly heResult = input<HistogramResult | null>(null);
  readonly plaintextResult = input<HistogramResult | null>(null);

  private readonly measurement = computed<HistogramResult | null>(
    () => this.heResult() ?? this.plaintextResult(),
  );

  /** Bin labels in order; both paths use the same bin layout, so either source works. */
  private readonly labels = computed<string[]>(
    () => this.measurement()?.bins.map((b) => b.label) ?? [],
  );

  readonly chartData = computed<ChartData<'bar', (number | null)[]>>(() => {
    const labels = this.labels();
    const datasets: ChartData<'bar', (number | null)[]>['datasets'] = [];

    const build = (result: HistogramResult | null, label: string, fill: string) => {
      if (!result) return;
      const byLabel = new Map(result.bins.map((b) => [b.label, b.count]));
      datasets.push({
        label,
        data: labels.map((l) => byLabel.get(l) ?? null),
        backgroundColor: fill,
      });
    };

    build(this.heResult(), 'HE', 'rgba(37, 99, 235, 0.75)');
    build(this.plaintextResult(), 'Plaintext', 'rgba(22, 163, 74, 0.75)');

    return { labels, datasets };
  });

  readonly chartOptions = computed<ChartConfiguration<'bar'>['options']>(() => {
    const unit = this.measurement()?.unitOfMeasurement;
    return {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          title: { display: true, text: unit ? `Value (${unit})` : 'Value' },
        },
        y: {
          beginAtZero: true,
          ticks: { precision: 0 },
          title: { display: true, text: 'Patients' },
        },
      },
      plugins: {
        legend: { position: 'bottom' },
        tooltip: {
          callbacks: {
            label: (ctx) => `${ctx.dataset.label}: ${ctx.parsed.y} patient(s)`,
          },
        },
      },
    };
  });

  /**
   * Values that fell outside the requested bins, so no patient silently disappears.
   * Each count is shown with its share of the total patients (in-range + out-of-range).
   */
  readonly edgeNote = computed<string | null>(() => {
    const m = this.measurement();
    if (!m || (m.belowRangeCount === 0 && m.aboveRangeCount === 0)) return null;

    const total =
      m.belowRangeCount + m.aboveRangeCount + m.bins.reduce((sum, b) => sum + b.count, 0);
    const share = (count: number) => (total > 0 ? Math.round((count / total) * 100) : 0);

    const parts: string[] = [];
    if (m.belowRangeCount > 0)
      parts.push(`${share(m.belowRangeCount)}% (${m.belowRangeCount}) below the first bin`);
    if (m.aboveRangeCount > 0)
      parts.push(`${share(m.aboveRangeCount)}% (${m.aboveRangeCount}) above the last bin`);
    return `Out of range: ${parts.join(' | ')}`;
  });

  readonly ariaLabel = computed<string>(() => {
    const m = this.measurement();
    if (!m) return 'Histogram (no data).';
    const bars = m.bins.map((b) => `${b.label}: ${b.count}`).join(', ');
    return `Frequency histogram of ${m.measurementName}, patients per bin: ${bars}.`;
  });
}
