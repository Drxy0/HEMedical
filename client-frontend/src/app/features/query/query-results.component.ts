import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import {
  BreakdownResult,
  HistogramResult,
  QueryResult,
} from '../../shared/models/clinical-measurement.model';
import { BreakdownChartComponent } from './breakdown-chart.component';
import { HistogramChartComponent } from './histogram-chart.component';

@Component({
  selector: 'app-query-results',
  imports: [DecimalPipe, BreakdownChartComponent, HistogramChartComponent],
  templateUrl: './query-results.component.html',
  styleUrl: './query-results.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueryResultsComponent {
  readonly heError = input<string | null>(null);
  readonly plaintextError = input<string | null>(null);
  readonly isLoadingHE = input(false);
  readonly isLoadingPlaintext = input(false);
  readonly heResult = input<QueryResult[] | null>(null);
  readonly plaintextResult = input<QueryResult[] | null>(null);
  readonly heBreakdown = input<BreakdownResult | null>(null);
  readonly plaintextBreakdown = input<BreakdownResult | null>(null);
  readonly heHistogram = input<HistogramResult | null>(null);
  readonly plaintextHistogram = input<HistogramResult | null>(null);

  readonly showPlaceholder = computed(
    () =>
      !this.isLoadingHE() &&
      !this.isLoadingPlaintext() &&
      this.heResult() === null &&
      this.plaintextResult() === null &&
      this.heBreakdown() === null &&
      this.plaintextBreakdown() === null &&
      this.heHistogram() === null &&
      this.plaintextHistogram() === null &&
      !this.heError() &&
      !this.plaintextError(),
  );
}
