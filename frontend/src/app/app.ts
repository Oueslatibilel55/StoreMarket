import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Footer } from './shared/layout/footer';
import { Header } from './shared/layout/header';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Header, Footer],
  host: { class: 'flex min-h-dvh flex-col' },
  template: `
    <app-header />
    <div class="flex-1">
      <router-outlet />
    </div>
    <app-footer />
  `,
})
export class App {}
