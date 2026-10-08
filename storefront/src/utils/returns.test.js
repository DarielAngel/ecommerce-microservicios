import { describe, it, expect } from 'vitest'
import { returnStatus, returnableLines, buildReturnRequest, reasonLabel, RETURN_REASONS } from './returns'

const order = {
  lines: [
    { variantId: 'v1', productName: 'Taza', quantity: 2, returnableQuantity: 2 },
    { variantId: 'v2', productName: 'Plato', quantity: 1, returnableQuantity: 0 }
  ]
}

describe('devoluciones', () => {
  it('describe cada estado para el cliente', () => {
    expect(returnStatus({ status: 'Requested' }).label).toMatch(/revisando/)
    expect(returnStatus({ status: 'Approved', refundAmount: 16 }).label).toBe('Aprobada — reembolso de $16.00 en proceso')
    expect(returnStatus({ status: 'Refunded', refundAmount: 16 })).toEqual({ label: 'Reembolsada: $16.00', tone: 'success' })
    expect(returnStatus({ status: 'Rejected' }).tone).toBe('danger')
  })

  it('solo ofrece las líneas que todavía tienen unidades para devolver', () => {
    expect(returnableLines(order).map((l) => l.variantId)).toEqual(['v1'])
  })

  it('arma la solicitud recortando cantidades al máximo y descartando ceros', () => {
    expect(buildReturnRequest(order, { v1: '5', v2: 1 }, 'Damaged', '  roto ')).toEqual({
      request: { items: [{ variantId: 'v1', quantity: 2 }], reason: 'Damaged', comment: 'roto' }
    })
  })

  it('pide al menos un producto, un motivo, y comentario si el motivo es "Otro"', () => {
    expect(buildReturnRequest(order, { v1: 0 }, 'Damaged', '').error).toMatch(/al menos un producto/)
    expect(buildReturnRequest(order, { v1: 1 }, '', '').error).toMatch(/motivo/)
    expect(buildReturnRequest(order, { v1: 1 }, 'Other', '  ').error).toMatch(/Cuéntanos/)
  })

  it('tiene un texto para cada motivo que acepta Órdenes', () => {
    expect(RETURN_REASONS.map((r) => r.value)).toEqual(['DoesNotFit', 'Damaged', 'WrongItem', 'NotAsDescribed', 'ChangedMind', 'Other'])
    expect(reasonLabel('Damaged')).toBe('Llegó dañado o con fallas')
  })

  it('describe las cancelaciones con su propio texto', () => {
    expect(returnStatus({ isCancellation: true, status: 'Requested' }).label).toBe('Cancelación solicitada — la estamos revisando')
    expect(returnStatus({ isCancellation: true, status: 'Refunded', refundAmount: 51 }).label).toBe('Pedido cancelado y reembolsado: $51.00')
    expect(returnStatus({ isCancellation: true, status: 'Rejected' }).tone).toBe('danger')
  })
})
