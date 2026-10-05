import defaultTheme from 'tailwindcss/defaultTheme'

// Colores semánticos: cambian con el modo (claro/oscuro) vía variables CSS (ver style.css),
// así ninguna vista necesita variantes `dark:` para sus superficies y textos.
const themed = (name) => `rgb(var(--${name}) / <alpha-value>)`

/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{vue,js}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Inter Variable"', ...defaultTheme.fontFamily.sans]
      },
      colors: {
        canvas: themed('canvas'),
        surface: themed('surface'),
        'surface-muted': themed('surface-muted'),
        line: themed('line'),
        ink: themed('ink'),
        'ink-soft': themed('ink-soft'),
        'ink-muted': themed('ink-muted'),
        brand: {
          50: '#ecfdf5', 100: '#d1fae5', 200: '#a7f3d0', 300: '#6ee7b7', 400: '#34d399',
          500: '#10b981', 600: '#059669', 700: '#047857', 800: '#065f46', 900: '#064e3b', 950: '#022c22',
          soft: themed('brand-soft'),
          ink: themed('brand-ink')
        },
        accent: { 400: '#fbbf24', 500: '#f59e0b', 600: '#d97706' }
      },
      boxShadow: {
        card: '0 1px 2px rgb(15 23 42 / 0.04), 0 4px 14px rgb(15 23 42 / 0.06)',
        'card-hover': '0 2px 4px rgb(15 23 42 / 0.06), 0 12px 28px rgb(15 23 42 / 0.12)'
      }
    }
  },
  plugins: []
}
