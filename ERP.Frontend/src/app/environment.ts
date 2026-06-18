export const environment = {
  production: false,

  apiUrl: '/api',

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
    payment:    '/payment',
    tenants:   '/tenants',
    plans:   '/plans',
  },
} as const;