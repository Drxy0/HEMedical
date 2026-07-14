import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  LOCALE_ID,
} from '@angular/core';
import { formatNumber } from '@angular/common';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { BreakdownResult } from '../../shared/models/clinical-measurement.model';
import { errorBarsPlugin, SigmaDatasetProps } from './error-bars.plugin';

/**
 * Grouped bar chart of a breakdown: one category per bucket (age group or time period),
 * HE in blue and the plaintext verification in green, each bar carrying ±1σ whiskers.
 * Empty buckets (no patients) render as gaps.
 */
@Component({
  selector: 'app-breakdown-chart',
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
      height: 22rem;
      margin-top: 1.5rem;
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
export class BreakdownChartComponent {
  readonly heResult = input<BreakdownResult | null>(null);
  readonly plaintextResult = input<BreakdownResult | null>(null);

  private readonly locale = inject(LOCALE_ID);

  /** Two-decimal number in the app locale (comma decimal separator), matching the results pipe. */
  private format2(value: number): string {
    return formatNumber(value, this.locale, '1.2-2');
  }

  readonly chartPlugins = [errorBarsPlugin];

  /** Bucket labels in order; both breakdowns share the same buckets, so either source works. */
  private readonly labels = computed<string[]>(() => {
    const source = this.heResult() ?? this.plaintextResult();
    return source?.buckets.map((b) => b.label) ?? [];
  });

  private readonly measurement = computed<BreakdownResult | null>(
    () => this.heResult() ?? this.plaintextResult(),
  );

  readonly chartData = computed<ChartData<'bar', (number | null)[]>>(() => {
    const labels = this.labels();
    const datasets: ChartData<'bar', (number | null)[]>['datasets'] = [];

    const build = (
      result: BreakdownResult | null,
      baseLabel: string,
      fill: string,
      whisker: string,
    ) => {
      if (!result) return;
      const hasSigma = result.buckets.some((b) => b.hasData && b.standardDeviation !== null);
      const byLabel = new Map(result.buckets.map((b) => [b.label, b]));
      datasets.push({
        label: hasSigma ? `${baseLabel} (±1σ)` : baseLabel,
        data: labels.map((l) => (byLabel.get(l)?.hasData ? byLabel.get(l)!.average : null)),
        backgroundColor: fill,
        standardDeviations: labels.map((l) =>
          byLabel.get(l)?.hasData ? byLabel.get(l)!.standardDeviation : null,
        ),
        whiskerColor: whisker,
      } as ChartData<'bar', (number | null)[]>['datasets'][number]);
    };

    build(this.heResult(), 'HE', 'rgba(37, 99, 235, 0.75)', '#1e3a8a');
    build(this.plaintextResult(), 'Plaintext', 'rgba(22, 163, 74, 0.75)', '#14532d');

    return { labels, datasets };
  });

  private readonly suggestedMax = computed<number | undefined>(() => {
    const buckets = [
      ...(this.heResult()?.buckets ?? []),
      ...(this.plaintextResult()?.buckets ?? []),
    ].filter((b) => b.hasData);
    if (!buckets.length) return undefined;
    return Math.max(...buckets.map((b) => b.average + (b.standardDeviation ?? 0))) * 1.1;
  });

  readonly chartOptions = computed<ChartConfiguration<'bar'>['options']>(() => {
    const unit = this.measurement()?.unitOfMeasurement;
    return {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        y: {
          beginAtZero: true,
          suggestedMax: this.suggestedMax(),
          title: { display: true, text: unit ? `Average (${unit})` : 'Average' },
        },
      },
      plugins: {
        legend: { position: 'bottom' },
        tooltip: {
          callbacks: {
            label: (ctx) => {
              const value = ctx.parsed.y;
              if (value == null) return `${ctx.dataset.label}: no data`;
              const sd = (ctx.dataset as SigmaDatasetProps).standardDeviations?.[ctx.dataIndex];
              return sd != null
                ? `${ctx.dataset.label}: ${this.format2(value)} ± ${this.format2(sd)}`
                : `${ctx.dataset.label}: ${this.format2(value)}`;
            },
          },
        },
      },
    };
  });

  readonly ariaLabel = computed<string>(() => {
    const m = this.measurement();
    if (!m) return 'Breakdown (no data).';
    const bars = m.buckets
      .filter((b) => b.hasData)
      .map((b) => `${b.label}: ${this.format2(b.average)}`)
      .join(', ');
    return `Breakdown of ${m.measurementName} by bucket, average per bucket: ${bars}.`;
  });
}
