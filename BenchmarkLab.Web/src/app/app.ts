import { Component, computed, signal } from '@angular/core';

interface Device {
  id: number;
  machineName: string;
  operatingSystem: string;
  cpuModel: string;
  totalMemoryGiB: number;
}

interface DevicePage {
  items: Device[];
  hasMore: boolean;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly devices = signal<Device[]>([]);
  protected readonly selectedId = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  protected readonly selectedDevice = computed(() =>
    this.devices().find(device => device.id === this.selectedId())
  );

  constructor() {
    void this.loadDevices();
  }

  protected async loadDevices(): Promise<void> {
    this.loading.set(true);
    this.error.set('');

    try {
      const devices: Device[] = [];
      let afterId = 0;

      while (true) {
        const response = await fetch(`/api/devices?afterId=${afterId}`);

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }

        const page: DevicePage = await response.json();
        devices.push(...page.items);

        if (!page.hasMore) break;
        afterId = page.items[page.items.length - 1].id;
      }

      this.devices.set(devices);
      this.selectedId.set(devices[0]?.id ?? null);
    } catch {
      this.error.set(
        'A géplista nem tölthető be. Ellenőrizd, hogy fut-e az API és a MariaDB.'
      );
    } finally {
      this.loading.set(false);
    }
  }
}