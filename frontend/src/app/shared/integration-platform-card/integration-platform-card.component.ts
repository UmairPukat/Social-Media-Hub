import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { IntegrationCardView } from '../integration-card.model';
import {
  integrationPlatformIcon,
  integrationShortDesc,
  integrationTone,
  supportsIntegrationConnectionDetails,
  supportsIntegrationPageSelection
} from '../integration-ui.utils';

@Component({
  selector: 'app-integration-platform-card',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './integration-platform-card.component.html'
})
export class IntegrationPlatformCardComponent {
  readonly card = input.required<IntegrationCardView>();
  readonly connecting = input<string | null>(null);
  readonly showAppActions = input(false);

  readonly connect = output<IntegrationCardView>();
  readonly disconnect = output<IntegrationCardView>();
  readonly changePage = output<IntegrationCardView>();
  readonly openDetails = output<IntegrationCardView>();
  readonly edit = output<IntegrationCardView>();
  readonly delete = output<IntegrationCardView>();

  tone(code: string): string {
    return integrationTone(code);
  }

  platformIcon(code: string): string {
    return integrationPlatformIcon(code);
  }

  shortDesc(text: string): string {
    return integrationShortDesc(text);
  }

  supportsPageSelection(code: string): boolean {
    return supportsIntegrationPageSelection(code);
  }

  supportsConnectionDetails(code: string): boolean {
    return supportsIntegrationConnectionDetails(code);
  }

  isConnecting(card: IntegrationCardView): boolean {
    return this.connecting() === card.connectingKey;
  }
}
