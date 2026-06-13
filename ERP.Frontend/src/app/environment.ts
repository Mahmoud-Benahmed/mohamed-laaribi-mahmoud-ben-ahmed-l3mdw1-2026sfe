export const environment = {
  production: true,

  apiUrl: 'http://erp.local',

  routes: {
    auth:        '/auth',
    roles:       '/auth/roles',
    controles:   '/auth/controles',
    privileges:  '/auth/privileges',
    articles:    '/articles',
    clients:     '/clients',
    stock:       '/stock',
    fournisseurs:'/fournisseurs',
    invoices:    '/invoices',
    tenants:   '/tenants',
  },
} as const;