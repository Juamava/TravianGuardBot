const admins = ['oslboreal@gmail.com', 'marcos98cgmailcom']

export function isAdminAccess(names: string[]) {
  return names.some(name => admins.some(admin => name.trim().localeCompare(admin, 'es', { sensitivity: 'accent' }) === 0))
}
